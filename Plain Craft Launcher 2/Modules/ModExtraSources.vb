Imports System.Security.Cryptography
Imports System.Text.RegularExpressions

'新增独立目录，不把不同平台的 ID 或安装格式冒充为 Modrinth / CurseForge。
Friend Module ModExtraSources
    Public Const FtbApi As String = "https://api.feed-the-beast.com/v1/modpacks/public/modpack/"
    Public Class ExtraProject
        Public Property Id As String
        Public Property Name As String
        Public Property Description As String
        Public Property PageUrl As String
        Public Overrides Function ToString() As String
            Return Name & vbCrLf & Description
        End Function
    End Class
    Public Function GitHubSearchAddress(Query As String) As String
        Query = Query.Trim()
        If Query.Length > 100 Then Throw New ArgumentException("搜索词最多 100 个字符。")
        If Regex.IsMatch(Query, "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") Then Return "https://api.github.com/repos/" & Query
        Return "https://api.github.com/search/repositories?per_page=20&q=" & Uri.EscapeDataString(If(Query = "", "minecraft mod", Query & " minecraft"))
    End Function
    Public Function FtbSearchAddress(Query As String) As String
        If Query.Length > 100 Then Throw New ArgumentException("搜索词最多 100 个字符。")
        Return FtbApi & If(String.IsNullOrWhiteSpace(Query), "featured/10", "search/10?term=" & Uri.EscapeDataString(Query.Trim()))
    End Function
    Public Function FetchExtraProjects(Provider As String, Query As String) As List(Of ExtraProject)
        Dim Result As New List(Of ExtraProject)
        Try
            If Provider = "GitHub" Then
                Dim Data = JObject.Parse(NetRequestByClient(GitHubSearchAddress(Query), RequireJson:=True))
                Dim Items As JArray = TryCast(Data("items"), JArray)
                If Items Is Nothing Then Items = New JArray(Data)
                For Each Item As JObject In Items
                    Dim Id = CStr(Item("full_name"))
                    If Not Regex.IsMatch(If(Id, ""), "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") Then Continue For
                    Result.Add(New ExtraProject With {.Id = Id, .Name = Id,
                        .Description = If(CStr(Item("description")), "无项目简介"), .PageUrl = "https://github.com/" & Id})
                Next
            ElseIf Provider = "FTB" Then
                Dim Data = JObject.Parse(NetRequestByClient(FtbSearchAddress(Query), RequireJson:=True))
                For Each Id As JToken In CType(Data("packs"), JArray).Take(10)
                    Dim Number = Id.Value(Of Integer)()
                    Dim Detail = JObject.Parse(NetRequestByClient(FtbApi & Number, RequireJson:=True))
                    Result.Add(New ExtraProject With {.Id = Number.ToString(), .Name = CStr(Detail("name")),
                        .Description = If(CStr(Detail("synopsis")), "FTB 整合包"),
                        .PageUrl = "https://www.feed-the-beast.com/modpacks/" & Number & "-" & Uri.EscapeDataString(If(CStr(Detail("slug")), ""))})
                Next
            Else
                Throw New ArgumentException("未知的资源来源。")
            End If
            Return Result
        Catch Ex As HttpRequestCodeException When Ex.StatusCode = 429 OrElse Ex.StatusCode = HttpStatusCode.Forbidden
            Throw New Exception(Provider & " 暂时限制了请求。请稍后重试；GitHub 可尝试直接输入作者名/项目名，或在浏览器打开官网。")
        End Try
    End Function

    Public Function IsAllowedGitHubAsset(Url As String, Repo As String, FileName As String) As Boolean
        Dim Address As Uri = Nothing
        Return Regex.IsMatch(Repo, "^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$") AndAlso
            Uri.TryCreate(Url, UriKind.Absolute, Address) AndAlso Address.Scheme = "https" AndAlso Address.IsDefaultPort AndAlso
            Address.Host = "github.com" AndAlso Address.UserInfo = "" AndAlso
            Address.AbsolutePath.StartsWith("/" & Repo & "/releases/download/", StringComparison.Ordinal) AndAlso
            FileName = Path.GetFileName(FileName) AndAlso FileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 AndAlso
            {".jar", ".zip", ".mrpack"}.Contains(Path.GetExtension(FileName).ToLowerInvariant())
    End Function

    Public Sub OpenExtraSources(Optional InitialProvider As String = "GitHub", Optional InitialQuery As String = "")
        Dim Window As New Window With {.Title = "更多资源来源 · LGCMcn", .Width = 790, .Height = 570, .MinWidth = 650, .MinHeight = 440,
            .Owner = FrmMain, .WindowStartupLocation = WindowStartupLocation.CenterOwner, .Background = Brushes.White}
        Dim Closed As Boolean = False
        AddHandler Window.Closed, Sub() Closed = True
        Dim Root As New DockPanel With {.Margin = New Thickness(20)}
        Dim Header As New StackPanel()
        DockPanel.SetDock(Header, Dock.Top) : Root.Children.Add(Header)
        Dim Tip As New TextBlock With {.Text = "GitHub：搜索项目、下载作者发布的 JAR/ZIP/MRPACK。FTB：搜索官方整合包并前往官网安装。两者均无需 API Key。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 12)}
        Header.Children.Add(Tip)
        Dim SearchRow As New DockPanel With {.Margin = New Thickness(0, 0, 0, 12)}
        Dim Provider As New ComboBox With {.Width = 105, .Margin = New Thickness(0, 0, 12, 0)}
        Provider.Items.Add("GitHub") : Provider.Items.Add("FTB") : Provider.SelectedItem = InitialProvider
        Dim Search As New Button With {.Content = "搜索", .Padding = New Thickness(18, 5, 18, 5), .Margin = New Thickness(12, 0, 0, 0)}
        DockPanel.SetDock(Provider, Dock.Left) : SearchRow.Children.Add(Provider)
        DockPanel.SetDock(Search, Dock.Right) : SearchRow.Children.Add(Search)
        Dim Input As New TextBox With {.Text = InitialQuery, .VerticalContentAlignment = VerticalAlignment.Center}
        SearchRow.Children.Add(Input) : Header.Children.Add(SearchRow)
        Dim Status As New TextBlock With {.Text = "输入关键词，GitHub 也支持直接输入 owner/repo。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 10)}
        Header.Children.Add(Status)
        Dim List As New ListBox()
        Dim Footer As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 12, 0, 0)}
        DockPanel.SetDock(Footer, Dock.Bottom) : Root.Children.Add(Footer)
        Dim OpenPage As New Button With {.Content = "打开官方项目页", .Padding = New Thickness(14, 7, 14, 7), .Margin = New Thickness(0, 0, 12, 0)}
        Dim Files As New Button With {.Content = "查看 GitHub 发布文件", .Padding = New Thickness(14, 7, 14, 7)}
        Footer.Children.Add(OpenPage) : Footer.Children.Add(Files) : Root.Children.Add(List)
        AddHandler Search.Click, Async Sub()
                                    Dim SelectedProvider = CStr(Provider.SelectedItem), Query = Input.Text
                                    Search.IsEnabled = False : Provider.IsEnabled = False : List.Items.Clear() : Status.Text = "正在查询 " & SelectedProvider & "…"
                                    Try
                                        Dim Projects = Await Task.Run(Function() FetchExtraProjects(SelectedProvider, Query))
                                        If Closed Then Return
                                        For Each Project In Projects
                                            Dim Item As New TextBlock With {.Text = Project.ToString(), .TextWrapping = TextWrapping.Wrap, .MaxWidth = 690}
                                            List.Items.Add(New ListBoxItem With {.Content = Item, .Tag = Project, .Padding = New Thickness(10)})
                                        Next
                                        Status.Text = $"{SelectedProvider}：找到 {Projects.Count} 个项目。请核对作者、游戏版本和加载器。"
                                    Catch Ex As Exception
                                        If Not Closed Then Status.Text = Ex.Message
                                    Finally
                                        If Not Closed Then Search.IsEnabled = True : Provider.IsEnabled = True
                                    End Try
                                End Sub
        AddHandler OpenPage.Click, Sub()
                                   If List.SelectedItem Is Nothing Then Return
                                   OpenWebsite(CType(CType(List.SelectedItem, ListBoxItem).Tag, ExtraProject).PageUrl)
                               End Sub
        AddHandler Files.Click, Sub()
                                    If List.SelectedItem Is Nothing Then Return
                                    Dim Project = CType(CType(List.SelectedItem, ListBoxItem).Tag, ExtraProject)
                                    If Project.PageUrl.StartsWith("https://github.com/", StringComparison.Ordinal) Then
                                        OpenGitHubReleaseFiles(Project.Id)
                                    Else
                                        MessageBox.Show(Window, "FTB 目录提供官方安装入口，尚未在本启动器实现 FTB 整合包自动安装。", "FTB 整合包")
                                    End If
                                End Sub
        Window.Content = Root : Window.ShowDialog()
    End Sub

    Private Async Sub OpenGitHubReleaseFiles(Repo As String)
        Try
            Dim Data = Await Task.Run(Function() JObject.Parse(NetRequestByClient("https://api.github.com/repos/" & Repo & "/releases/latest", RequireJson:=True)))
            Dim Window As New Window With {.Title = Repo & " · 最新发布文件", .Width = 680, .Height = 420, .Owner = FrmMain, .WindowStartupLocation = WindowStartupLocation.CenterOwner}
            Dim Panel As New StackPanel With {.Margin = New Thickness(20)}
            Panel.Children.Add(New TextBlock With {.Text = "下载后不会自动运行或安装。请核对作者、游戏版本与加载器；无服务端哈希的文件只核对大小。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 12)})
            For Each Asset As JObject In CType(Data("assets"), JArray)
                Dim FileName = CStr(Asset("name")), Url = CStr(Asset("browser_download_url"))
                If Not IsAllowedGitHubAsset(Url, Repo, FileName) Then Continue For
                Dim Item As JObject = CType(Asset.DeepClone(), JObject)
                Dim Button As New Button With {.Content = FileName, .Padding = New Thickness(10, 6, 10, 6), .Margin = New Thickness(0, 0, 0, 8)}
                AddHandler Button.Click, Async Sub()
                                             Dim Dialog As New Microsoft.Win32.SaveFileDialog With {.FileName = FileName, .Filter = "发布文件|*" & Path.GetExtension(FileName)}
                                             If Dialog.ShowDialog(Window) <> True Then Return
                                             Button.IsEnabled = False
                                             Dim Temp = Dialog.FileName & "." & Guid.NewGuid().ToString("N") & ".download"
                                             Try
                                                 Await Task.Run(Sub()
                                                                    NetDownloadByLoader(Url, Temp, Check:=New FileChecker With {.ActualSize = CLng(Item("size"))})
                                                                    Dim Hash = CStr(Item("digest"))
                                                                    If Hash IsNot Nothing AndAlso Hash.StartsWith("sha256:", StringComparison.Ordinal) Then
                                                                        Using Algorithm = SHA256.Create(), Stream = File.OpenRead(Temp)
                                                                            If BitConverter.ToString(Algorithm.ComputeHash(Stream)).Replace("-", "").ToLowerInvariant() <> Hash.Substring(7).ToLowerInvariant() Then Throw New IOException("SHA256 校验失败。")
                                                                        End Using
                                                                    End If
                                                                End Sub)
                                                 AtomicReplaceLocal(Temp, Dialog.FileName)
                                                 MessageBox.Show("下载完成：" & Dialog.FileName, "发布文件")
                                             Catch Ex As Exception
                                                 MessageBox.Show(Ex.Message, "下载失败")
                                             Finally
                                                 If File.Exists(Temp) Then File.Delete(Temp)
                                                 Button.IsEnabled = True
                                             End Try
                                         End Sub
                Panel.Children.Add(Button)
            Next
            If Panel.Children.Count = 1 Then Panel.Children.Add(New TextBlock With {.Text = "最新 Release 没有可下载的 JAR、ZIP 或 MRPACK；可前往作者项目页查看其他版本。"})
            Window.Content = New ScrollViewer With {.Content = Panel, .VerticalScrollBarVisibility = ScrollBarVisibility.Auto}
            Window.ShowDialog()
        Catch Ex As Exception
            MessageBox.Show("读取发布文件失败：" & Ex.Message, "GitHub Releases")
        End Try
    End Sub
End Module
