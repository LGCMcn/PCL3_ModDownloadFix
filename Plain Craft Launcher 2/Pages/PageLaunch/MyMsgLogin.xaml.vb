Public Class MyMsgLogin
    Private Data As JObject
    Private UserCode As String '需要用户在网页上输入的设备代码
    Private DeviceCode As String '用于轮询的设备代码
    Private Website As String '验证网页的网址

#Region "弹窗"

    Private ReadOnly MyConverter As MyMsgBoxConverter
    Private ReadOnly Uuid As Integer = GetUuid()

    Public Sub New(Converter As MyMsgBoxConverter)
        Try
            InitializeComponent()
            Btn1.Name = Btn1.Name & GetUuid()
            Btn2.Name = Btn2.Name & GetUuid()
            Btn3.Name = Btn3.Name & GetUuid()
            MyConverter = Converter
            ShapeLine.StrokeThickness = GetWPFSize(1)
            Data = Converter.Content
            Init()
        Catch ex As Exception
            Logger.Error(ex, "登录弹窗初始化失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub Load(sender As Object, e As EventArgs) Handles MyBase.Loaded
        Try
            '动画
            Opacity = 0
            AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, If(MyConverter.IsWarn, New MyColor(140, 80, 0, 0), New MyColor(90, 0, 0, 0)) - FrmMain.PanMsg.Background, 200), "PanMsg Background")
            AniStart({
                AaOpacity(Me, 1, 120, 60),
                AaDouble(Sub(i) TransformPos.Y += i, -TransformPos.Y, 300, 60, New AniEaseOutBack(AniEasePower.Weak)),
                AaDouble(Sub(i) TransformRotate.Angle += i, -TransformRotate.Angle, 300, 60, New AniEaseOutFluent(AniEasePower.Weak))
            }, "MyMsgBox " & Uuid)
            '记录日志
            Logger.Info($"登录弹窗：{LabTitle.Text}{vbCrLf}{LabCaption.Text}")
        Catch ex As Exception
            Logger.Error(ex, "登录弹窗加载失败", LogBehavior.Toast)
        End Try
    End Sub
    Private Sub Close()
        '动画
        AniStart({
            AaCode(
            Sub()
                If Not WaitingMyMsgBox.Any() Then
                    AniStart(AaColor(FrmMain.PanMsg, Grid.BackgroundProperty, New MyColor(0, 0, 0, 0) - FrmMain.PanMsg.Background, 200, Ease:=New AniEaseOutFluent(AniEasePower.Weak)))
                End If
            End Sub, 30),
            AaOpacity(Me, -Opacity, 80, 20),
            AaDouble(Sub(i) TransformPos.Y += i, 20 - TransformPos.Y, 150, 0, New AniEaseOutFluent),
            AaDouble(Sub(i) TransformRotate.Angle += i, 6 - TransformRotate.Angle, 150, 0, New AniEaseInFluent(AniEasePower.Weak)),
            AaCode(Sub() CType(Parent, Grid).Children.Remove(Me), , True)
        }, "MyMsgBox " & Uuid)
    End Sub

    '实现回车和 Esc 的接口（#4857）
    Public Sub Btn1_Click() Handles Btn1.Click
    End Sub
    Public Sub Btn3_Click() Handles Btn3.Click
        Finished(New OperationCanceledException)
    End Sub

    Private Sub Drag(sender As Object, e As MouseButtonEventArgs) Handles PanBorder.MouseLeftButtonDown, LabTitle.MouseLeftButtonDown
        On Error Resume Next
        If e.GetPosition(ShapeLine).Y <= 2 Then FrmMain.DragMove()
    End Sub

#End Region

    Private Sub Finished(Result As Object)
        If MyConverter.IsExited Then Return
        MyConverter.IsExited = True
        MyConverter.Result = Result
        RunInUi(AddressOf Close)
        Thread.Sleep(200)
        FrmMain.ShowWindowToTop()
    End Sub

    Private Sub Init()
        UserCode = Data("user_code")
        DeviceCode = Data("device_code")
        Website = Data("verification_uri")
        If Not IsMicrosoftVerificationUri(Website) Then Throw New Exception("微软授权返回了非受信任的登录网址，已停止打开网页。")
        '设置 UI
        LabTitle.Text = "登录 Minecraft"
        LabCaption.Text =
            $"登录网页将自动开启，请在网页中输入 {UserCode}（已自动复制）。" & vbCrLf & vbCrLf &
            $"如果网络环境不佳，网页可能一直加载不出来，届时请使用加速器或 VPN 改善网络环境。" & vbCrLf &
            $"你也可以用其他设备打开 {Website} 并输入上述代码。"
        CustomEventService.SetEventData(Btn1, Website)
        CustomEventService.SetEventData(Btn2, UserCode)
        '启动工作线程
        RunInNewThread(AddressOf WorkThread, "MyMsgLogin")
    End Sub

    Private Sub WorkThread()
        Dim Lifetime = Math.Max(1, Math.Min(1800, If(Data("expires_in")?.Value(Of Integer)(), 900)))
        Dim PollSeconds = Math.Max(1, If(Data("interval")?.Value(Of Integer)(), 5))
        Dim Clock = Stopwatch.StartNew()
        Dim UnknownFailureCount As Integer = 0
        If MyConverter.IsExited Then Return
        OpenWebsite(Website)
        ClipboardSet(UserCode)
        Do While Not MyConverter.IsExited
            Dim Due = Clock.Elapsed.TotalSeconds + PollSeconds
            While Clock.Elapsed.TotalSeconds < Due
                If MyConverter.IsExited Then Return
                If Clock.Elapsed.TotalSeconds >= Lifetime Then
                    Finished(New Exception("$登录代码已过期，请重新开始登录。"))
                    Return
                End If
                Thread.Sleep(100)
            End While
            Try
                Dim Result = NetRequestByClient(MicrosoftTokenEndpoint, HttpMethod.Post,
                    Content:=MicrosoftForm("poll", DeviceCode), ContentType:="application/x-www-form-urlencoded",
                    Timeout:=15000, MakeLog:=False, RequireJson:=True)
                Dim ResultJson = JObject.Parse(Result)
                Hint("网页登录成功！", HintType.Green)
                Finished({CStr(ResultJson("access_token")), CStr(ResultJson("refresh_token"))})
                Return
            Catch Ex As HttpRequestCodeException
                Dim ErrorCode As String = ""
                Try
                    ErrorCode = CStr(JObject.Parse(Ex.Response)("error"))
                Catch
                End Try
                Select Case ErrorCode
                    Case "authorization_pending"
                        Continue Do
                    Case "slow_down"
                        PollSeconds = DevicePollingDelay(PollSeconds, "slow_down")
                        Continue Do
                    Case "authorization_declined", "access_denied"
                        Finished(New Exception("$你取消或拒绝了此次登录授权。"))
                        Return
                    Case "expired_token", "bad_verification_code", "invalid_grant"
                        Finished(New Exception("$登录代码已失效，请重新开始登录。"))
                        Return
                    Case "invalid_client", "unauthorized_client"
                        Finished(New Exception("$本项目的 Microsoft Client ID 无效或没有启用公共客户端设备授权，请检查应用注册。"))
                        Return
                End Select
                UnknownFailureCount += 1
                PollSeconds = DevicePollingDelay(PollSeconds, "slow_down")
                If UnknownFailureCount >= 3 Then
                    Finished(New Exception("$微软登录轮询失败，请检查网络和应用配置后重新登录。"))
                    Return
                End If
            Catch Ex As Exception
                UnknownFailureCount += 1
                If UnknownFailureCount >= 3 Then
                    Finished(New Exception("$无法连接微软登录服务，请检查网络和系统证书后重试。"))
                    Return
                End If
            End Try
        Loop
    End Sub
End Class
