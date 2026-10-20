Public Class PageOtherAbout

    Private Shadows IsLoaded As Boolean = False
    Private Sub PageOtherAbout_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", VersionDisplay).Replace("%VERSIONCODE%", VersionCode).Replace("%BRANCH%", CInt(BuildType))
        If BuildType = BuildTypes.Debug Then
            BtnDonateDonate.Visibility = Visibility.Collapsed
            BtnDonateOutput.Visibility = Visibility.Visible
        End If
        RefreshProjectRelease()

    End Sub

    Public Shared Sub CopyIdentify() Handles BtnDonateCopy.Click
        ClipboardSet(Identify)
    End Sub
    Private Sub BtnDonateCodeInput_Click() Handles BtnDonateInput.Click
        InputPotatoCode(False)
    End Sub
    Private Sub BtnDonateOutput_Click(sender As Object, e As EventArgs) Handles BtnDonateOutput.Click
        GeneratePotatoCode()
    End Sub
    Private Sub BtnAboutUpdate_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnAboutUpdate.Click
        RefreshProjectRelease()
    End Sub

    Private LatestProjectRelease As ModProjectUpdate.ProjectRelease
    Private CheckingProjectRelease As Boolean
    Private InstallingProjectRelease As Boolean
    Private Async Sub RefreshProjectRelease()
        If CheckingProjectRelease OrElse InstallingProjectRelease Then Return
        CheckingProjectRelease = True : BtnAboutUpdate.IsEnabled = False : BtnProjectInstall.IsEnabled = False
        TextProjectVersion.Text = "本机第三方版本：v" & DownloadFixVersion & " · 正在获取项目最新发行版…"
        Try
            LatestProjectRelease = Await FetchProjectRelease()
            TextProjectVersion.Text = "本机：v" & DownloadFixVersion & "  |  最新发行版：" & LatestProjectRelease.Tag &
                If(LatestProjectRelease.IsNewer, "（可更新）", If(LatestProjectRelease.Version = ParseProjectVersion(DownloadFixVersion), "（已是最新版）", "（本机版本较新）"))
            TextProjectChanges.Text = LatestProjectRelease.Notes
            BtnProjectInstall.IsEnabled = LatestProjectRelease.IsNewer AndAlso LatestProjectRelease.AssetUrl IsNot Nothing
            TextProjectUpdateStatus.Text = If(LatestProjectRelease.AssetUrl Is Nothing, "此历史发行版不支持自动更新，可点击发行版详情下载。", "更新会退出并重新启动程序；保留服务配置、账号和游戏目录，旧程序备份在 .pcl-update-backups。")
        Catch Ex As Exception
            LatestProjectRelease = Nothing
            TextProjectVersion.Text = "本机第三方版本：v" & DownloadFixVersion & " · 暂时无法获取最新发行版"
            TextProjectChanges.Text = "获取失败，请点击检查更新重试。"
            TextProjectUpdateStatus.Text = Ex.Message
        Finally
            CheckingProjectRelease = False : BtnAboutUpdate.IsEnabled = True
        End Try
    End Sub

    Private Async Sub BtnProjectInstall_Click() Handles BtnProjectInstall.Click
        If InstallingProjectRelease OrElse LatestProjectRelease Is Nothing Then Return
        If HasDownloadingTask() OrElse McLoginMsLoader.State = LoadState.Loading OrElse McLaunchLoader.State = LoadState.Loading Then
            TextProjectUpdateStatus.Text = "请等待下载、登录或游戏启动任务完成后再更新。" : Return
        End If
        InstallingProjectRelease = True : BtnProjectInstall.IsEnabled = False : BtnAboutUpdate.IsEnabled = False
        Try
            Dim Reporter As New Progress(Of String)(Sub(Value) TextProjectUpdateStatus.Text = Value)
            Dim PlanPath = Await PrepareProjectUpdate(LatestProjectRelease, Reporter)
            TextProjectUpdateStatus.Text = "校验通过，即将退出、替换程序并重新启动…"
            StartPreparedProjectUpdate(PlanPath)
        Catch Ex As Exception
            TextProjectUpdateStatus.Text = "更新未完成：" & Ex.Message
            BtnProjectInstall.IsEnabled = True : BtnAboutUpdate.IsEnabled = True : InstallingProjectRelease = False
        End Try
    End Sub

    Private Sub BtnProjectRelease_Click() Handles BtnProjectRelease.Click
        OpenWebsite(If(LatestProjectRelease?.Url, "https://github.com/" & ProjectRepository & "/releases/latest"))
    End Sub

End Class
