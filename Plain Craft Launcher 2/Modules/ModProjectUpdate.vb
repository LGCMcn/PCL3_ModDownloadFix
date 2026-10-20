Imports System.Security.Cryptography
Imports System.Text.RegularExpressions
Imports System.Xml.Linq

'本分支仅跟踪维护者项目的正式发行版，不连接原作者的更新服务。
Friend Module ModProjectUpdate
    Public Const ProjectRepository As String = "LGCMcn/PCL3_ModDownloadFix"
    Public Const ProjectReleaseApi As String = "https://api.github.com/repos/" & ProjectRepository & "/releases/latest"
    Public Const ReleaseExecutable As String = "Plain Craft Launcher (PCL) LGCMcn 下载修复版.exe"
    Public Const ReleaseArchive As String = "PCL.DownloadFix.Windows-x64.zip"
    Private Const MaximumPackage As Long = 256L * 1024 * 1024
    Private Const MaximumExpanded As Long = 512L * 1024 * 1024

    Public Class ProjectRelease
        Public Property Tag As String
        Public Property Version As Version
        Public Property Notes As String
        Public Property Url As String
        Public Property AssetUrl As String
        Public Property Size As Long
        Public Property Sha256 As String
        Public Property ChecksumUrl As String
        Public ReadOnly Property IsNewer As Boolean
            Get
                Return Version > ParseProjectVersion(DownloadFixVersion)
            End Get
        End Property
    End Class

    Public Function ParseProjectVersion(Value As String) As Version
        If Not Regex.IsMatch(If(Value, ""), "^v?\d{1,5}\.\d{1,5}\.\d{1,5}(\.\d{1,5})?$") Then Throw New FormatException("发行版版本号格式无效。")
        Dim Parsed = New Version(Value.TrimStart("v"c))
        Return New Version(Parsed.Major, Parsed.Minor, Parsed.Build, Math.Max(0, Parsed.Revision))
    End Function

    Public Function FormatProjectNotes(Body As String) As String
        Dim Lines As New List(Of String)
        For Each Line In If(Body, "").Split({vbCrLf, vbLf}, StringSplitOptions.None)
            Dim Found = Regex.Match(Line, "^\s*(?:[-*]\s+)?\[\s*([+-])\s*\]\s*(.+)$")
            If Found.Success Then Lines.Add("[ " & Found.Groups(1).Value & " ] " & Found.Groups(2).Value.Trim())
        Next
        Return If(Lines.Count = 0, "该历史发行版尚未提供规范的更新记录，可点击「发行版详情」查看。", String.Join(vbCrLf, Lines))
    End Function

    Public Function IsProjectAssetUrl(Value As String, Tag As String, Name As String) As Boolean
        Dim Address As Uri = Nothing
        Return Uri.TryCreate(Value, UriKind.Absolute, Address) AndAlso Address.Scheme = "https" AndAlso Address.IsDefaultPort AndAlso
            Address.Host = "github.com" AndAlso Address.UserInfo = "" AndAlso Address.Query = "" AndAlso Address.Fragment = "" AndAlso
            Address.AbsolutePath = "/" & ProjectRepository & "/releases/download/" & Uri.EscapeDataString(Tag) & "/" & Name
    End Function

    Public Function ParseProjectRelease(Data As JObject) As ProjectRelease
        If Data.Value(Of Boolean)("draft") OrElse Data.Value(Of Boolean)("prerelease") Then Throw New FormatException("不自动安装草稿或预发行版。")
        Dim Result As New ProjectRelease With {.Tag = CStr(Data("tag_name")), .Notes = FormatProjectNotes(CStr(Data("body")))}
        Dim VersionName = Result.Tag
        If Not Regex.IsMatch(If(VersionName, ""), "^v?\d+\.\d+\.\d+(\.\d+)?$") Then
            VersionName = Regex.Match(If(CStr(Data("name")), ""), "^v\d+\.\d+\.\d+").Value
        End If
        Result.Version = ParseProjectVersion(VersionName)
        Result.Url = "https://github.com/" & ProjectRepository & "/releases/tag/" & Uri.EscapeDataString(Result.Tag)
        For Each Asset As JObject In CType(Data("assets"), JArray)
            Dim Name = CStr(Asset("name")), Address = CStr(Asset("browser_download_url"))
            If Name <> ReleaseArchive AndAlso Name <> "SHA256SUMS.txt" Then Continue For
            If Not IsProjectAssetUrl(Address, Result.Tag, Name) Then Throw New FormatException("更新附件地址不属于本项目。")
            If Name = ReleaseArchive Then
                If Result.AssetUrl IsNot Nothing Then Throw New FormatException("运行包附件重复。")
                Result.AssetUrl = Address : Result.Size = Asset.Value(Of Long)("size")
                Dim Digest = If(CStr(Asset("digest")), "")
                If Regex.IsMatch(Digest, "^sha256:[a-fA-F0-9]{64}$") Then Result.Sha256 = Digest.Substring(7).ToLowerInvariant()
                If Result.Size <= 0 OrElse Result.Size > MaximumPackage Then Throw New FormatException("更新包大小超出限制。")
            Else
                Result.ChecksumUrl = Address
            End If
        Next
        Return Result
    End Function

    '独立客户端不带账号、API Key、Cookie 或公共磁盘缓存。跳转只允许 GitHub 附件域名。
    Private Async Function ReadUpdateFile(Address As String, Destination As Stream, Limit As Long, Progress As IProgress(Of String)) As Task(Of Long)
        Using Handler As New HttpClientHandler With {.AllowAutoRedirect = False, .UseCookies = False}, Client As New HttpClient(Handler) With {.Timeout = TimeSpan.FromMinutes(8)}
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-LGCMcn-DownloadFix/" & DownloadFixVersion)
            Client.DefaultRequestHeaders.CacheControl = New System.Net.Http.Headers.CacheControlHeaderValue With {.NoCache = True}
            For Hop = 0 To 5
                Dim AddressUri As New Uri(Address)
                If AddressUri.Scheme <> "https" OrElse Not AddressUri.IsDefaultPort OrElse AddressUri.UserInfo <> "" OrElse
                    Not {"api.github.com", "github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com"}.Contains(AddressUri.Host) Then
                    Throw New IOException("更新下载被重定向到不支持的地址。")
                End If
                Using Response = Await Client.GetAsync(AddressUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(False)
                    If CInt(Response.StatusCode) >= 300 AndAlso CInt(Response.StatusCode) < 400 Then
                        If Response.Headers.Location Is Nothing Then Throw New IOException("更新下载跳转缺少地址。")
                        Address = New Uri(AddressUri, Response.Headers.Location).AbsoluteUri : Continue For
                    End If
                    If Response.StatusCode = HttpStatusCode.Forbidden OrElse CInt(Response.StatusCode) = 429 Then Throw New IOException("GitHub 请求受限，请稍后重新检查或打开项目发行版页面。")
                    Response.EnsureSuccessStatusCode()
                    If Response.Content.Headers.ContentLength.GetValueOrDefault() > Limit Then Throw New IOException("更新文件超过大小限制。")
                    Using Source = Await Response.Content.ReadAsStreamAsync().ConfigureAwait(False)
                        Dim Buffer(81919) As Byte, Total As Long = 0
                        Do
                            Dim Count = Await Source.ReadAsync(Buffer, 0, Buffer.Length).ConfigureAwait(False)
                            If Count = 0 Then Exit Do
                            Total += Count
                            If Total > Limit Then Throw New IOException("更新文件超过大小限制。")
                            Await Destination.WriteAsync(Buffer, 0, Count).ConfigureAwait(False)
                            Progress?.Report("正在下载更新：" & (Total / 1048576).ToString("F1") & " MB")
                        Loop
                        Return Total
                    End Using
                End Using
            Next
        End Using
        Throw New IOException("更新下载跳转次数过多。")
    End Function

    Public Async Function FetchProjectRelease() As Task(Of ProjectRelease)
        Using Content As New MemoryStream()
            Await ReadUpdateFile(ProjectReleaseApi, Content, 1024 * 1024, Nothing).ConfigureAwait(False)
            Return ParseProjectRelease(JObject.Parse(Encoding.UTF8.GetString(Content.ToArray())))
        End Using
    End Function

    Public Function UpdateFileHash(FileName As String) As String
        Using Input = File.OpenRead(FileName), Hasher = SHA256.Create()
            Return BitConverter.ToString(Hasher.ComputeHash(Input)).Replace("-", "").ToLowerInvariant()
        End Using
    End Function

    Public Function IsUpdatePackageFile(Name As String) As Boolean
        Return {ReleaseExecutable, ReleaseExecutable & ".config", "PCL.Update.exe", "LICENCE", "第三方版本声明.md", "使用说明.md", "项目分析与验证.md",
            "PCL.Release.json", "SHA256SUMS.json", "PCL.Services.json", "PCL.Link.json", "组件/easytier-windows-x86_64-v2.6.4.zip"}.Contains(Name, StringComparer.Ordinal)
    End Function

    Public Function ExtractVerifiedUpdate(ZipPath As String, Folder As String, ExpectedVersion As String) As List(Of String)
        Directory.CreateDirectory(Folder)
        Dim Names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase), Total As Long = 0
        Using Archive = ZipFile.OpenRead(ZipPath)
            For Each Entry In Archive.Entries
                If Not IsUpdatePackageFile(Entry.FullName) OrElse Not Names.Add(Entry.FullName) Then Throw New IOException("更新包包含不支持或重复的路径：" & Entry.FullName)
                Total += Entry.Length
                If Entry.Length > MaximumPackage OrElse Total > MaximumExpanded Then Throw New IOException("更新包解压大小超出限制。")
                If {"SHA256SUMS.json", "PCL.Release.json"}.Contains(Entry.FullName) AndAlso Entry.Length > 256 * 1024 Then Throw New IOException("更新包元数据过大。")
                Dim Target = Path.Combine(Folder, Entry.FullName.Replace("/"c, Path.DirectorySeparatorChar))
                Directory.CreateDirectory(Path.GetDirectoryName(Target))
                Entry.ExtractToFile(Target, False)
            Next
        End Using
        If Not {ReleaseExecutable, ReleaseExecutable & ".config", "PCL.Update.exe", "PCL.Release.json", "SHA256SUMS.json", "LICENCE"}.All(Function(Name) Names.Contains(Name)) Then Throw New IOException("更新包缺少必要文件。")
        Dim Metadata = JObject.Parse(File.ReadAllText(Path.Combine(Folder, "PCL.Release.json")))
        If CStr(Metadata("repository")) <> ProjectRepository OrElse CStr(Metadata("version")) <> ExpectedVersion Then Throw New IOException("更新包版本或项目标识不匹配。")
        Dim Hashes = JObject.Parse(File.ReadAllText(Path.Combine(Folder, "SHA256SUMS.json")))
        For Each Name In Names.Where(Function(Value) Value <> "SHA256SUMS.json")
            Dim Expected = CStr(Hashes(Name))
            If Not Regex.IsMatch(If(Expected, ""), "^[a-f0-9]{64}$") OrElse UpdateFileHash(Path.Combine(Folder, Name)) <> Expected Then Throw New IOException("更新包内文件校验失败：" & Name)
        Next
        Return Names.Where(Function(Name) Not {"PCL.Services.json", "PCL.Link.json"}.Contains(Name)).ToList()
    End Function

    Public Async Function PrepareProjectUpdate(Release As ProjectRelease, Progress As IProgress(Of String)) As Task(Of String)
        If Not Release.IsNewer OrElse Release.AssetUrl Is Nothing Then Throw New IOException("没有可自动安装的更新。")
        Dim Helper = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCL.Update.exe")
        If Not File.Exists(Helper) Then Throw New IOException("本机缺少 PCL.Update.exe，请先完整解压运行包。")
        Dim Stage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCLDownloadFix", "Updates", Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Stage)
        Try
            Dim Expected = Release.Sha256
            If Expected Is Nothing Then
                If Release.ChecksumUrl Is Nothing Then Throw New IOException("发行版未提供 SHA256，已停止自动更新。")
                Using Content As New MemoryStream()
                    Await ReadUpdateFile(Release.ChecksumUrl, Content, 256 * 1024, Nothing).ConfigureAwait(False)
                    Dim Found = Regex.Match(Encoding.UTF8.GetString(Content.ToArray()), "(?m)^([a-fA-F0-9]{64})  " & Regex.Escape(ReleaseArchive) & "\r?$")
                    If Not Found.Success Then Throw New IOException("未找到运行包的 SHA256。")
                    Expected = Found.Groups(1).Value.ToLowerInvariant()
                End Using
            End If
            Dim Package = Path.Combine(Stage, "package.zip")
            Using Output = New FileStream(Package, FileMode.CreateNew, FileAccess.Write)
                Dim Size = Await ReadUpdateFile(Release.AssetUrl, Output, Release.Size, Progress).ConfigureAwait(False)
                If Size <> Release.Size Then Throw New IOException("更新包下载不完整。")
            End Using
            If UpdateFileHash(Package) <> Expected Then Throw New IOException("更新包 SHA256 校验失败，未替换任何文件。")
            Progress?.Report("正在校验与准备更新…")
            Dim Payload = Path.Combine(Stage, "payload")
            Dim Files = ExtractVerifiedUpdate(Package, Payload, Release.Version.ToString(3))
            Dim CurrentExe = Process.GetCurrentProcess().MainModule.FileName
            If Path.GetDirectoryName(CurrentExe).TrimEnd(Path.DirectorySeparatorChar) <> AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) Then Throw New IOException("程序路径不匹配。")
            Dim Plan As New XElement("update", New XAttribute("directory", AppDomain.CurrentDomain.BaseDirectory),
                New XAttribute("executable", Path.GetFileName(CurrentExe)), New XAttribute("pid", Process.GetCurrentProcess().Id),
                New XAttribute("start", Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks))
            For Each Name In Files
                Dim Destination = Name
                If Name.StartsWith(ReleaseExecutable, StringComparison.Ordinal) Then Destination = Path.GetFileName(CurrentExe) & Name.Substring(ReleaseExecutable.Length)
                Plan.Add(New XElement("file", New XAttribute("source", Name), New XAttribute("target", Destination), New XAttribute("sha256", UpdateFileHash(Path.Combine(Payload, Name)))))
            Next
            Dim PlanPath = Path.Combine(Stage, "plan.xml")
            Plan.Save(PlanPath)
            File.Copy(Helper, Path.Combine(Stage, "PCL.Update.exe"))
            Return PlanPath
        Catch
            Try
                Directory.Delete(Stage, True)
            Catch
            End Try
            Throw
        End Try
    End Function

    Public Sub StartPreparedProjectUpdate(PlanPath As String)
        If HasDownloadingTask() OrElse McLoginMsLoader.State = LoadState.Loading OrElse McLaunchLoader.State = LoadState.Loading Then Throw New IOException("请等待下载、登录或游戏启动任务完成后更新。")
        Dim Helper = Path.Combine(Path.GetDirectoryName(PlanPath), "PCL.Update.exe")
        Process.Start(New ProcessStartInfo(Helper, """" & PlanPath & """") With {.UseShellExecute = False, .CreateNoWindow = True, .WorkingDirectory = Path.GetDirectoryName(PlanPath)})
        FrmMain.EndProgram(False)
    End Sub
End Module
