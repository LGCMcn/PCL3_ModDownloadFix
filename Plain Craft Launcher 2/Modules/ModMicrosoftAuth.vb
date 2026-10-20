Imports System.Security.Cryptography

'官方设备授权协议；不使用其他启动器的 Client ID，不代理账号令牌。
Friend Module ModMicrosoftAuth
    <Runtime.InteropServices.DllImport("kernel32.dll", CharSet:=Runtime.InteropServices.CharSet.Unicode, SetLastError:=True)>
    Private Function MoveFileEx(Existing As String, Destination As String, Flags As Integer) As Boolean
    End Function
    Public Sub AtomicReplaceLocal(Temporary As String, Destination As String)
        'MoveFileEx handles replacement in EFS-encrypted Windows user folders too.
        If Not MoveFileEx(Temporary, Destination, 1 Or 2) Then
            Throw New ComponentModel.Win32Exception(Runtime.InteropServices.Marshal.GetLastWin32Error(), "无法替换本地文件，错误码 " & Runtime.InteropServices.Marshal.GetLastWin32Error())
        End If
    End Sub
    Public Const MicrosoftDeviceEndpoint As String = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode"
    Public Const MicrosoftTokenEndpoint As String = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token"
    Public Const MinecraftApplicationHelp As String = "https://aka.ms/AppRegInfo"

    Public Function NeedsPrivateTransport(Request As HttpRequestMessage) As Boolean
        Return Request.Headers.Contains("Authorization") OrElse Request.Headers.Contains("x-api-key") OrElse
            {"login.microsoftonline.com", "login.live.com", "user.auth.xboxlive.com", "xsts.auth.xboxlive.com", "api.minecraftservices.com"}.Contains(Request.RequestUri.Host.ToLowerInvariant())
    End Function
    Public Function SafeRedirectGet(Original As HttpRequestMessage, Destination As Uri) As HttpRequestMessage
        If Original.Method <> HttpMethod.Get OrElse Original.Headers.Contains("Authorization") Then
            Throw New HttpRequestException("带账号凭据的接口发生重定向，已停止请求。")
        End If
        If Destination.Scheme <> "https" OrElse Destination.UserInfo <> "" Then Throw New HttpRequestException("拒绝向非 HTTPS 地址重定向。")
        Dim NextRequest As New HttpRequestMessage(HttpMethod.Get, Destination)
        For Each Name In {"User-Agent", "Accept", "Range"}
            If Original.Headers.Contains(Name) Then NextRequest.Headers.TryAddWithoutValidation(Name, Original.Headers.GetValues(Name))
        Next
        'Only add a key again when the new target itself passes the original allowlist.
        SecretHeadersSign(Destination.AbsoluteUri, NextRequest)
        Return NextRequest
    End Function

    Public Function MicrosoftForm(Grant As String, Optional Token As String = "") As String
        Dim Fields As New List(Of KeyValuePair(Of String, String)) From {
            New KeyValuePair(Of String, String)("client_id", OAuthClientId)}
        If Grant = "device" Then
            Fields.Add(New KeyValuePair(Of String, String)("scope", "XboxLive.signin offline_access"))
        ElseIf Grant = "refresh" Then
            Fields.Add(New KeyValuePair(Of String, String)("grant_type", "refresh_token"))
            Fields.Add(New KeyValuePair(Of String, String)("refresh_token", Token))
            Fields.Add(New KeyValuePair(Of String, String)("scope", "XboxLive.signin offline_access"))
        ElseIf Grant = "poll" Then
            Fields.Add(New KeyValuePair(Of String, String)("grant_type", "urn:ietf:params:oauth:grant-type:device_code"))
            Fields.Add(New KeyValuePair(Of String, String)("device_code", Token))
        Else
            Throw New ArgumentException("未知的微软授权步骤。")
        End If
        Using Content As New FormUrlEncodedContent(Fields)
            Return Content.ReadAsStringAsync().GetAwaiter().GetResult()
        End Using
    End Function

    Public Function DevicePollingDelay(CurrentSeconds As Integer, ErrorCode As String) As Integer
        Return Math.Max(1, CurrentSeconds) + If(ErrorCode = "slow_down", 5, 0)
    End Function

    Public Function IsMicrosoftVerificationUri(Value As String) As Boolean
        Dim Address As Uri = Nothing
        Return Uri.TryCreate(Value, UriKind.Absolute, Address) AndAlso Address.Scheme = "https" AndAlso
            Address.IsDefaultPort AndAlso Address.UserInfo = "" AndAlso
            {"microsoft.com", "www.microsoft.com", "login.microsoftonline.com", "login.live.com"}.Contains(Address.Host.ToLowerInvariant())
    End Function

    Public Function MinecraftLoginFailure(Status As Integer, Response As String) As String
        If If(Response, "").IndexOf("Invalid app registration", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return "$本项目的 Microsoft Client ID 尚未获得 Minecraft Services 授权。请由项目维护者申请应用接入；注册 Entra 应用本身不等于已获 Minecraft API 授权。说明：https://aka.ms/AppRegInfo"
        End If
        If Status = 429 Then Return "$登录请求过于频繁，请等待几分钟后再试。"
        If Status = 403 Then Return "$Minecraft Services 拒绝登录。可能涉及应用接入资格、账号状态或网络限制；请先核对本项目的 Client ID 与应用授权，不能只据此判断为 IP 异常。"
        If Status = 503 Then Return "$Minecraft 登录服务暂时不可用，请稍后重试。"
        Return Nothing
    End Function

    'DPAPI 绑定当前 Windows 用户。读取旧设置时兼容旧密文；新保存一律使用此格式。
    Public Function ProtectLocal(Value As String) As String
        Return "dpapi:" & Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(Value),
            Encoding.UTF8.GetBytes("LGCMcn.PCLDownloadFix.LocalSecrets.v1"), DataProtectionScope.CurrentUser))
    End Function
    Public Function UnprotectLocal(Value As String) As String
        If Not Value.StartsWith("dpapi:", StringComparison.Ordinal) Then Throw New CryptographicException("未知的本地凭据格式。")
        Return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(Value.Substring(6)),
            Encoding.UTF8.GetBytes("LGCMcn.PCLDownloadFix.LocalSecrets.v1"), DataProtectionScope.CurrentUser))
    End Function
End Module
