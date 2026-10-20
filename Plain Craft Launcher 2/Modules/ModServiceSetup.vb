Friend Module ModServiceSetup
    Public Function IsCurseForgeReady() As Boolean
        Return Not String.IsNullOrWhiteSpace(CurseForgeAPIKey) OrElse ServiceBase("ModMirrorBase", "") <> "" OrElse
            ServiceBase("CurseForgeApiBase", "https://api.curseforge.com") <> "https://api.curseforge.com"
    End Function
    Public Sub OpenServiceSetup()
        Dim Window As New Window With {.Title = "服务接入配置 · LGCMcn", .Width = 650, .Height = 470,
            .Owner = FrmMain, .WindowStartupLocation = WindowStartupLocation.CenterOwner, .Background = Brushes.White}
        Dim Panel As New StackPanel With {.Margin = New Thickness(24)}
        Panel.Children.Add(New TextBlock With {.Text = "微软登录需要本项目自己的 Client ID 与 Minecraft API 接入资格。CurseForge 需要申请 Key；Modrinth、GitHub 和 FTB 无需 Key。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 16)})
        Panel.Children.Add(New TextBlock With {.Text = "Microsoft Application (client) ID", .Margin = New Thickness(0, 0, 0, 7)})
        Dim Client As New TextBox With {.Text = ServiceValue("MicrosoftClientId", ""), .Padding = New Thickness(5), .Margin = New Thickness(0, 0, 0, 15)}
        Panel.Children.Add(Client)
        Panel.Children.Add(New TextBlock With {.Text = "CurseForge API Key（留空保留原值；不会显示已有 Key）", .Margin = New Thickness(0, 0, 0, 7)})
        Dim Key As New PasswordBox With {.Padding = New Thickness(5), .Margin = New Thickness(0, 0, 0, 15)}
        Panel.Children.Add(Key)
        Dim Links As New WrapPanel With {.Margin = New Thickness(0, 0, 0, 16)}
        For Each Pair In New Dictionary(Of String, String) From {
            {"注册微软应用", "https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade"},
            {"Minecraft 接入申请", MinecraftApplicationHelp},
            {"CurseForge Key 申请", "https://support.curseforge.com/support/solutions/articles/9000208346-about-the-curseforge-api-and-how-to-apply-for-a-key"}}
            Dim Target = Pair.Value
            Dim Button As New Button With {.Content = Pair.Key, .Padding = New Thickness(10, 6, 10, 6), .Margin = New Thickness(0, 0, 10, 8)}
            AddHandler Button.Click, Sub() OpenWebsite(Target)
            Links.Children.Add(Button)
        Next
        Panel.Children.Add(Links)
        Panel.Children.Add(New TextBlock With {.Text = "Client ID 是可公开的应用标识，不是账号密码。Key 用当前 Windows 用户加密保存；修改后重启。环境变量仍优先于此文件。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 15)})
        Dim Save As New Button With {.Content = "保存本地配置", .Padding = New Thickness(14, 8, 14, 8), .HorizontalAlignment = HorizontalAlignment.Left}
        AddHandler Save.Click, Sub()
                                   Try
                                       Dim Id As Guid
                                       If Client.Text.Trim() <> "" AndAlso Not Guid.TryParse(Client.Text.Trim(), Id) Then Throw New ArgumentException("Client ID 应为应用页面提供的 GUID，不能填写邮箱、密码或 Client Secret。")
                                       Dim Config = If(File.Exists(ServiceConfigPath), JObject.Parse(File.ReadAllText(ServiceConfigPath, Encoding.UTF8)), New JObject())
                                       Config("MicrosoftClientId") = Client.Text.Trim()
                                       If Key.Password <> "" Then
                                           Config("CurseForgeApiKeyProtected") = ProtectLocal(Key.Password.Trim())
                                           Config.Remove("CurseForgeApiKey")
                                       End If
                                       Dim Temp = ServiceConfigPath & "." & Guid.NewGuid().ToString("N") & ".tmp"
                                       File.WriteAllText(Temp, Config.ToString(), New UTF8Encoding(False))
                                       AtomicReplaceLocal(Temp, ServiceConfigPath)
                                       Key.Clear()
                                       MessageBox.Show(Window, "配置已保存。重启启动器后生效；应用注册和 API 授权仍需服务方审核。", "保存完成")
                                       Window.Close()
                                   Catch Ex As Exception
                                       MessageBox.Show(Window, Ex.Message, "无法保存")
                                   End Try
                               End Sub
        Panel.Children.Add(Save)
        Window.Content = New ScrollViewer With {.Content = Panel, .VerticalScrollBarVisibility = ScrollBarVisibility.Auto}
        Window.ShowDialog()
    End Sub
End Module
