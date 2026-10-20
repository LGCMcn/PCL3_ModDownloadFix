param([switch]$Network)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot 'Plain Craft Launcher 2\bin'
if (-not (Test-Path (Join-Path $output 'Plain Craft Launcher 2.exe'))) { throw '请先执行 build.ps1。' }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$compiler = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
if (-not $compiler) { throw '未找到 Visual Studio C# 编译器。' }
$testRoot = Join-Path $repoRoot ('artifacts\smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
Copy-Item (Join-Path $output '*.exe*') $testRoot
Copy-Item (Join-Path $output '*.dll') $testRoot
Copy-Item (Join-Path $repoRoot 'Plain Craft Launcher 2\Resources\Newtonsoft.Json.dll') $testRoot
Copy-Item (Join-Path $repoRoot 'Plain Craft Launcher 2\App.config') (Join-Path $testRoot 'SmokeTests.exe.config')
$wpfRefs = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
& $compiler /nologo "/out:$testRoot\SmokeTests.exe" "/r:$testRoot\Newtonsoft.Json.dll" /r:System.Net.Http.dll "/r:$wpfRefs\PresentationFramework.dll" "/r:$wpfRefs\PresentationCore.dll" "/r:$wpfRefs\WindowsBase.dll" (Join-Path $PSScriptRoot 'SmokeTests.cs')
if ($LASTEXITCODE -ne 0) { throw '测试程序编译失败。' }

# 测试进程只接收合成配置，避免意外读取 CI 或本机的真实个人凭据。
$savedMsId = $env:PCL_MS_CLIENT_ID
$savedCfKey = $env:PCL_CURSEFORGE_API_KEY
try {
    Remove-Item Env:PCL_MS_CLIENT_ID -ErrorAction SilentlyContinue
    Remove-Item Env:PCL_CURSEFORGE_API_KEY -ErrorAction SilentlyContinue
    $arguments = @()
    if ($Network) { $arguments += '--network' }
    & (Join-Path $testRoot 'SmokeTests.exe') @arguments
    if ($LASTEXITCODE -ne 0) { throw '默认配置或网络检查失败。' }
    & (Join-Path $testRoot 'SmokeTests.exe') --personalization
    if ($LASTEXITCODE -ne 0) { throw '第三方个性化检查失败。' }
    @{
        MicrosoftClientId = 'local-test-client-id'
        CurseForgeApiKey = 'local-test-key'
        UnrelatedSecret = 'must-never-be-exported'
        BmclApiBase = 'https://bmcl.example.test/base'
        ModMirrorBase = 'https://mirror.example.test'
        ModrinthApiBase = 'https://api.modrinth.com'
        CurseForgeApiBase = 'https://cf.example.test/api'
    } | ConvertTo-Json | Set-Content (Join-Path $testRoot 'PCL.Services.json') -Encoding UTF8
    & (Join-Path $testRoot 'SmokeTests.exe') --custom-config
    if ($LASTEXITCODE -ne 0) { throw '自定义配置检查失败。' }
    Set-Content (Join-Path $testRoot 'PCL.Services.json') '{ invalid json' -Encoding UTF8
    & (Join-Path $testRoot 'SmokeTests.exe') --invalid-config
    if ($LASTEXITCODE -ne 0) { throw '错误配置检查失败。' }
} finally {
    $env:PCL_MS_CLIENT_ID = $savedMsId
    $env:PCL_CURSEFORGE_API_KEY = $savedCfKey
}
Write-Output "检查通过，测试文件保存在：$testRoot"
