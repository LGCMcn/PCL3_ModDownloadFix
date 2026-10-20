Imports System.Security.Cryptography

'账号按 Minecraft UUID 与本项目 Client ID 区分；不读取其他启动器账号。
Friend Module ModAccounts
    Private ReadOnly Gate As New Object()
    Public ReadOnly AccountStorePath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCLDownloadFix", "Accounts", InstallAccountKey() & ".dat")
    Private Function InstallAccountKey() As String
        Using Hash = SHA256.Create()
            Return BitConverter.ToString(Hash.ComputeHash(Encoding.UTF8.GetBytes(AppDomain.CurrentDomain.BaseDirectory.ToLowerInvariant()))).Replace("-", "").Substring(0, 24)
        End Using
    End Function
    Public Function ReadAccounts() As JArray
        SyncLock Gate
            If Not File.Exists(AccountStorePath) Then Return New JArray()
            Try
                Return JArray.Parse(UnprotectLocal(File.ReadAllText(AccountStorePath, Encoding.UTF8)))
            Catch
                Throw New Exception("无法解密本机账号列表。请使用保存这些账号的 Windows 用户；原文件已保留，不会自动清空。")
            End Try
        End SyncLock
    End Function
    Private Sub WriteAccounts(Accounts As JArray)
        Directory.CreateDirectory(Path.GetDirectoryName(AccountStorePath))
        Dim Temp = AccountStorePath & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            File.WriteAllText(Temp, ProtectLocal(Accounts.ToString(0)), New UTF8Encoding(False))
            AtomicReplaceLocal(Temp, AccountStorePath)
        Finally
            If File.Exists(Temp) Then File.Delete(Temp)
        End Try
    End Sub
    Public Function MergeAccount(Accounts As JArray, Account As JObject) As JArray
        Dim Result = CType(Accounts.DeepClone(), JArray)
        Dim Uuid = CStr(Account("Uuid")), Client = CStr(Account("ClientId"))
        If String.IsNullOrWhiteSpace(Uuid) OrElse String.IsNullOrWhiteSpace(CStr(Account("Name"))) Then Throw New ArgumentException("账号缺少 UUID 或玩家名。")
        For Each Old As JObject In Result.ToList()
            If CStr(Old("Uuid")) = Uuid AndAlso CStr(Old("ClientId")) = Client Then Result.Remove(Old)
        Next
        Result.Add(Account.DeepClone())
        Return Result
    End Function
    Public Sub SaveCurrentAccount()
        Dim Uuid = Settings.Get(Of String)("CacheMsV2Uuid")
        Dim Refresh = Settings.Get(Of String)("CacheMsV2OAuthRefresh")
        If Uuid = "" OrElse Refresh = "" Then Return
        Dim Account As New JObject From {
            {"Uuid", Uuid}, {"Name", Settings.Get(Of String)("CacheMsV2Name")},
            {"ClientId", Settings.Get(Of String)("CacheMsV2ClientId")},
            {"Refresh", Refresh}, {"Access", Settings.Get(Of String)("CacheMsV2Access")},
            {"Profile", Settings.Get(Of String)("CacheMsV2ProfileJson")},
            {"Expires", Settings.Get(Of Long)("CacheMsV2Expires")}}
        SyncLock Gate
            WriteAccounts(MergeAccount(ReadAccounts(), Account))
        End SyncLock
    End Sub
    Private Sub EnsureAccountIdle()
        If McLoginMsLoader.State = LoadState.Loading OrElse McLaunchLoader.State = LoadState.Loading Then
            Throw New InvalidOperationException("正在登录或启动游戏，请完成后再管理账号。")
        End If
    End Sub
    Public Sub SelectStoredAccount(Uuid As String, ClientId As String)
        EnsureAccountIdle()
        Dim Account = ReadAccounts().OfType(Of JObject)().FirstOrDefault(Function(A) CStr(A("Uuid")) = Uuid AndAlso CStr(A("ClientId")) = ClientId)
        If Account Is Nothing Then Throw New ArgumentException("未找到该账号。")
        If String.IsNullOrEmpty(ClientId) OrElse ClientId <> OAuthClientId Then
            Throw New InvalidOperationException("该账号使用其他或未知 Client ID 登录。请用本项目当前 Client ID 重新网页登录，原账号记录已保留。")
        End If
        '列表已经持久保存，切换只修改当前账号，不发起网络请求。
        McLoginMsLoader.Cancel()
        Settings.Set("CacheMsV2Uuid", CStr(Account("Uuid")))
        Settings.Set("CacheMsV2Name", CStr(Account("Name")))
        Settings.Set("CacheMsV2OAuthRefresh", CStr(Account("Refresh")))
        Settings.Set("CacheMsV2Access", CStr(Account("Access")))
        Settings.Set("CacheMsV2ProfileJson", CStr(Account("Profile")))
        Settings.Set("CacheMsV2ClientId", ClientId)
        Settings.Set("CacheMsV2Expires", CLng(Account("Expires")))
        If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.RefreshPage(False, True)
    End Sub
    Public Sub RemoveStoredAccount(Uuid As String, ClientId As String)
        EnsureAccountIdle()
        SyncLock Gate
            Dim Accounts = ReadAccounts()
            Dim Removed As New List(Of String)
            For Each Account As JObject In Accounts.ToList()
                If CStr(Account("Uuid")) = Uuid AndAlso CStr(Account("ClientId")) = ClientId Then
                    Removed.Add(CStr(Account("Name")))
                    Accounts.Remove(Account)
                End If
            Next
            WriteAccounts(Accounts)
            Dim Legacy = JObject.Parse(Settings.Get(Of String)("LoginMsJson"))
            For Each Name In Removed
                Legacy.Remove(Name)
            Next
            Settings.Set("LoginMsJson", Legacy.ToString(0))
            If Settings.Get(Of String)("CacheMsV2Uuid") = Uuid AndAlso Settings.Get(Of String)("CacheMsV2ClientId") = ClientId Then ClearActiveAccount()
        End SyncLock
    End Sub
    Public Sub ClearActiveAccount()
        McLoginMsLoader.Cancel()
        For Each Key In {"OAuthRefresh", "Access", "ProfileJson", "Uuid", "Name", "ClientId"}
            Settings.Set("CacheMsV2" & Key, "")
        Next
        Settings.Set("CacheMsV2Expires", 0L)
        If FrmLaunchLeft IsNot Nothing Then FrmLaunchLeft.RefreshPage(False, True)
    End Sub

    Public Sub OpenAccountManager()
        Try
            EnsureAccountIdle()
            SaveCurrentAccount()
            Dim Window As New Window With {.Title = "微软多账号管理 · LGCMcn", .Width = 620, .Height = 420,
                .MinWidth = 540, .MinHeight = 340, .Owner = FrmMain, .WindowStartupLocation = WindowStartupLocation.CenterOwner,
                .Background = Brushes.White}
            Dim Layout As New DockPanel With {.Margin = New Thickness(22)}
            Dim Tip As New TextBlock With {.Text = "账号保存在本机并由 Windows 加密。移除只删除本机记录；如需撤销授权，请在微软账户页面操作。", .TextWrapping = TextWrapping.Wrap, .Margin = New Thickness(0, 0, 0, 16)}
            DockPanel.SetDock(Tip, Dock.Top) : Layout.Children.Add(Tip)
            Dim Actions As New WrapPanel With {.Margin = New Thickness(0, 16, 0, 0)}
            DockPanel.SetDock(Actions, Dock.Bottom) : Layout.Children.Add(Actions)
            Dim List As New ListBox With {.DisplayMemberPath = "Content"}
            Dim Reload As Action = Sub()
                                       List.Items.Clear()
                                       For Each Account As JObject In ReadAccounts()
                                           List.Items.Add(New ListBoxItem With {.Content = CStr(Account("Name")) & "   ·   " & CStr(Account("Uuid")), .Tag = Account, .Padding = New Thickness(10)})
                                       Next
                                   End Sub
            Dim Switch As New Button With {.Content = "切换选中账号", .Padding = New Thickness(12, 7, 12, 7), .Margin = New Thickness(0, 0, 10, 0)}
            Dim Add As New Button With {.Content = "添加并网页登录", .Padding = New Thickness(12, 7, 12, 7), .Margin = New Thickness(0, 0, 10, 0)}
            Dim Remove As New Button With {.Content = "移除本机记录", .Padding = New Thickness(12, 7, 12, 7)}
            AddHandler Switch.Click, Sub()
                                         Try
                                             If List.SelectedItem Is Nothing Then Return
                                             Dim A = CType(CType(List.SelectedItem, ListBoxItem).Tag, JObject)
                                             SelectStoredAccount(CStr(A("Uuid")), CStr(A("ClientId")))
                                             Window.Close()
                                         Catch Ex As Exception
                                             MessageBox.Show(Window, Ex.Message, "切换账号")
                                         End Try
                                     End Sub
            AddHandler Add.Click, Sub()
                                      Try
                                          EnsureAccountIdle()
                                          ClearActiveAccount()
                                          Window.Close()
                                          If FrmLoginMs IsNot Nothing Then FrmLoginMs.ComboAccounts.SelectedIndex = 0
                                          If FrmLoginMs IsNot Nothing Then FrmLoginMs.BeginNewLogin()
                                      Catch Ex As Exception
                                          MessageBox.Show(Ex.Message, "添加账号")
                                      End Try
                                  End Sub
            AddHandler Remove.Click, Sub()
                                         Try
                                             If List.SelectedItem Is Nothing Then Return
                                             Dim A = CType(CType(List.SelectedItem, ListBoxItem).Tag, JObject)
                                             If MessageBox.Show(Window, "从本机移除 " & CStr(A("Name")) & " 的保存记录？", "移除账号", MessageBoxButton.YesNo) <> MessageBoxResult.Yes Then Return
                                             RemoveStoredAccount(CStr(A("Uuid")), CStr(A("ClientId")))
                                             Reload()
                                         Catch Ex As Exception
                                             MessageBox.Show(Window, Ex.Message, "移除账号")
                                         End Try
                                     End Sub
            Actions.Children.Add(Switch) : Actions.Children.Add(Add) : Actions.Children.Add(Remove)
            Layout.Children.Add(List) : Window.Content = Layout : Reload() : Window.ShowDialog()
        Catch Ex As Exception
            MessageBox.Show(Ex.Message, "账号管理")
        End Try
    End Sub
End Module
