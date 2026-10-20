'第三方独立服务配置：不依赖 PCL 配置服务器，不包含任何官方私钥。
Friend Module ModServices
    Public ReadOnly ServiceConfigPath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCL.Services.json")
    Private ReadOnly ServiceConfig As New Lazy(Of JObject)(AddressOf ReadServiceConfig)

    Private Function ReadServiceConfig() As JObject
        If Not File.Exists(ServiceConfigPath) Then Return New JObject()
        Try
            Return JObject.Parse(File.ReadAllText(ServiceConfigPath, Encoding.UTF8))
        Catch ex As Exception
            Throw New Exception("无法读取 PCL.Services.json，请检查 JSON 格式后重启启动器。", ex)
        End Try
    End Function

    Public Function ServiceValue(Name As String, DefaultValue As String, Optional EnvironmentName As String = Nothing) As String
        Dim Value = If(EnvironmentName Is Nothing, Nothing, Environment.GetEnvironmentVariable(EnvironmentName))
        If Not String.IsNullOrWhiteSpace(Value) Then Return Value.Trim()
        Return If(ServiceConfig.Value(Name)?.Value(Of String)(), DefaultValue).Trim()
    End Function

    Public Function ServiceBase(Name As String, DefaultValue As String) As String
        Dim Value = ServiceValue(Name, DefaultValue).TrimEnd("/"c)
        If Value = "" Then Return "" '空字符串禁用此镜像
        Dim Address As Uri = Nothing
        If Not Uri.TryCreate(Value, UriKind.Absolute, Address) OrElse Address.Scheme <> Uri.UriSchemeHttps OrElse
           Address.UserInfo <> "" OrElse Address.Query <> "" OrElse Address.Fragment <> "" Then
            Throw New Exception("PCL.Services.json 中的 " & Name & " 必须为无查询参数的 HTTPS 地址。")
        End If
        Return Value
    End Function

    '仅匹配完整源站前缀，避免替换路径、查询参数中的域名。
    Public Function ReplaceServiceBase(Url As String, Original As String, Replacement As String) As String
        If Replacement = "" Then Return Url
        If Url.Equals(Original, StringComparison.OrdinalIgnoreCase) Then Return Replacement
        If Url.StartsWith(Original & "/", StringComparison.OrdinalIgnoreCase) Then Return Replacement & Url.Substring(Original.Length)
        Return Url
    End Function

    Public Function ResolveServiceUrl(Url As String) As String
        Url = ReplaceServiceBase(Url, "https://bmclapi2.bangbang93.com", ServiceBase("BmclApiBase", "https://bmclapi2.bangbang93.com"))
        Url = ReplaceServiceBase(Url, "https://mod.mcimirror.top", ServiceBase("ModMirrorBase", ""))
        Url = ReplaceServiceBase(Url, "https://api.modrinth.com", ServiceBase("ModrinthApiBase", "https://api.modrinth.com"))
        Return ReplaceServiceBase(Url, "https://api.curseforge.com", ServiceBase("CurseForgeApiBase", "https://api.curseforge.com"))
    End Function

    Public Function IsCurseForgeApi(Url As String) As Boolean
        Dim Address As Uri = Nothing
        If Not Uri.TryCreate(Url, UriKind.Absolute, Address) Then Return False
        Dim Configured = New Uri(If(ServiceBase("CurseForgeApiBase", "https://api.curseforge.com") = "", "https://api.curseforge.com", ServiceBase("CurseForgeApiBase", "https://api.curseforge.com")))
        Return Address.Scheme = Uri.UriSchemeHttps AndAlso Address.Authority.Equals(Configured.Authority, StringComparison.OrdinalIgnoreCase) AndAlso
            (Address.AbsolutePath = Configured.AbsolutePath.TrimEnd("/"c) OrElse Address.AbsolutePath.StartsWith(Configured.AbsolutePath.TrimEnd("/"c) & "/", StringComparison.Ordinal))
    End Function

    Public Function IsCurseForgeDownload(Url As String) As Boolean
        Dim Address As Uri = Nothing
        If Not Uri.TryCreate(Url, UriKind.Absolute, Address) OrElse Address.Scheme <> Uri.UriSchemeHttps Then Return False
        Return {"edge.forgecdn.net", "mediafilez.forgecdn.net", "media.forgecdn.net"}.Contains(Address.Host.ToLowerInvariant())
    End Function

    '仅公开允许的字段随整合包导出；不复制密钥和未来可能新增的私人字段。
    Public Sub ExportPublicServiceConfig(Destination As String)
        If Not File.Exists(ServiceConfigPath) Then Return
        Dim PublicConfig As New JObject()
        For Each Name In {"MicrosoftClientId", "BmclApiBase", "ModMirrorBase", "ModrinthApiBase", "CurseForgeApiBase"}
            If ServiceConfig.Value(Name) IsNot Nothing Then PublicConfig(Name) = ServiceConfig.Value(Name).DeepClone()
        Next
        File.WriteAllText(Destination, PublicConfig.ToString(), New UTF8Encoding(False))
    End Sub

    Public Sub LoadIndependentLinkConfig()
        Dim ConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCL.Link.json")
        If File.Exists(ConfigPath) Then
            ServerConfig = JObject.Parse(File.ReadAllText(ConfigPath, Encoding.UTF8))
        Else
            ServerConfig = JObject.Parse("{ 'Link': { 'EasyTierVersion': 20604, 'Argument': '', 'DiscoverPeerId': 94, 'DiscoverPeer': 'tcp://mc1.easytier.cn:55558', 'Peers': ['tcp://mc1.easytier.cn:55558'], 'PeersBlackList': [], 'RandomPeer': 2, 'MandatoryPeers': [], 'NodesApi': 'https://uptime.easytier.cn/api/nodes?page=1&per_page=1000', 'Downloads': ['https://github.com/EasyTier/EasyTier/releases/download/v2.6.4/easytier-windows-{arch}-v2.6.4.zip'], 'Hashes': { 'x86_64': '27af91e270e554709b048bd32327fefd2dfce5062ae1e8701af7550c6f525f84', 'i686': 'bf557daeccc5525d95b8a230c339d75554fb52d82d1e050e9a5202c92c02e09e', 'arm64': '37023f8a3451c9234b17ee2089a03dc344ce90d803b5b359cb6c46682b0549b4' } } }")
        End If
        Dim Link = TryCast(ServerConfig("Link"), JObject)
        If Link Is Nothing Then Throw New Exception("PCL.Link.json 缺少 Link 配置。")
        For Each Name In {"EasyTierVersion", "Downloads", "Argument", "DiscoverPeerId", "DiscoverPeer", "Peers", "PeersBlackList", "RandomPeer", "MandatoryPeers"}
            If Link(Name) Is Nothing Then Throw New Exception("PCL.Link.json 缺少 Link." & Name)
        Next
    End Sub

    Public Function IndependentNewsHome() As String
        Dim P As XNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        Dim L As XNamespace = "clr-namespace:PCL"
        Dim Body As New XElement(P + "StackPanel", New XAttribute("Margin", "25,37,25,15"))
        Body.Add(New XElement(P + "TextBlock", New XAttribute("Text", "Minecraft 官方新闻与版本信息"), New XAttribute("Margin", "0,0,0,12")))
        Try
            Dim Manifest As JObject = NetRequestByClientRetry("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
                BackupUrl:="https://bmclapi2.bangbang93.com/mc/game/version_manifest.json", RequireJson:=True).DeserializeJson()
            Body.Add(New XElement(P + "TextBlock", New XAttribute("Text", "最新正式版：" & Manifest("latest")("release").ToString()), New XAttribute("Margin", "0,0,0,8")))
            Body.Add(New XElement(P + "TextBlock", New XAttribute("Text", "最新快照版：" & Manifest("latest")("snapshot").ToString()), New XAttribute("Margin", "0,0,0,12")))
        Catch ex As Exception
            Logger.Warn(ex, "获取 Minecraft 版本新闻失败，仍可打开官方新闻页面。")
        End Try
        Body.Add(New XElement(L + "MyButton", New XAttribute("Text", "阅读 Minecraft 官方新闻"), New XAttribute("Height", "35"),
            New XAttribute(L + "CustomEventService.EventType", "打开网页"),
            New XAttribute(L + "CustomEventService.EventData", "https://www.minecraft.net/zh-hans/articles")))
        Return New XElement(L + "MyCard", New XAttribute("Title", "Minecraft 新闻"), Body).ToString()
    End Function

    Public Sub OpenServiceConfig()
        Try
            If Not File.Exists(ServiceConfigPath) Then
                File.WriteAllText(ServiceConfigPath, New JObject From {
                    {"MicrosoftClientId", ""}, {"CurseForgeApiKey", ""},
                    {"BmclApiBase", "https://bmclapi2.bangbang93.com"},
                    {"ModMirrorBase", ""},
                    {"ModrinthApiBase", "https://api.modrinth.com"},
                    {"CurseForgeApiBase", "https://api.curseforge.com"}
                }.ToString(), New UTF8Encoding(False))
            End If
            Process.Start(New ProcessStartInfo("notepad.exe", """" & ServiceConfigPath & """"))
            Hint("保存配置后，请重启启动器使修改生效。")
        Catch ex As Exception
            Logger.Error(ex, "打开独立服务配置失败", LogBehavior.Alert)
        End Try
    End Sub
End Module
