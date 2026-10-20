param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$privateSdk = Join-Path (Split-Path $repoRoot -Parent) 'tools\dotnet'
if (Test-Path (Join-Path $privateSdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $privateSdk
    $env:PATH = "$privateSdk;$env:PATH"
}
$dotnetPath = (Get-Command dotnet.exe -ErrorAction Stop).Source
$sdkVersion = (& $dotnetPath --version).Trim()
if ($LASTEXITCODE -ne 0) { throw '需要 .NET SDK 9 或更新版本。' }
$sdkRoot = Split-Path $dotnetPath -Parent
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = $sdkRoot
$env:MSBuildSDKsPath = Join-Path $sdkRoot "sdk\$sdkVersion\Sdks"
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuildPath = if (Test-Path $vswhere) { & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1 }
if (-not $msbuildPath) {
    $msbuildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($msbuildCommand) { $msbuildPath = $msbuildCommand.Source }
}
if (-not $msbuildPath) { throw '需要 Visual Studio Build Tools 的 .NET 桌面构建工具和 .NET Framework 4.8 Targeting Pack。' }
& $dotnetPath restore (Join-Path $repoRoot 'PCLCS\PCLCS.csproj') --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'NuGet 还原失败。' }
& $msbuildPath (Join-Path $repoRoot 'Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj') "/p:Configuration=$Configuration" /m /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw '构建失败。' }
$compiler = Join-Path (Split-Path $msbuildPath -Parent) 'Roslyn\csc.exe'
& $compiler /nologo /target:winexe /r:System.Core.dll /r:System.Xml.Linq.dll /r:System.Windows.Forms.dll "/out:$repoRoot\Plain Craft Launcher 2\bin\PCL.Update.exe" (Join-Path $repoRoot 'scripts\Updater.cs')
if ($LASTEXITCODE -ne 0) { throw '更新工具构建失败。' }
Write-Output (Join-Path $repoRoot 'Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe')
