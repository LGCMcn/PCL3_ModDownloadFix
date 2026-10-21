Public Class PageOtherTest
    Private ReadingDeviceInfo As Boolean
    Private Sub DevicePageLoaded() Handles Me.Loaded, Me.NavigationActivated
        ApplyQuietCards(Me)
        SettingService.RefreshSettings(Me)
        If Not ReadingDeviceInfo Then RefreshDeviceInfo(False)
    End Sub
    Private Sub BtnDeviceRefresh_Click() Handles BtnDeviceRefresh.Click
        RefreshDeviceInfo(True)
    End Sub
    Private Async Sub RefreshDeviceInfo(Refresh As Boolean)
        If ReadingDeviceInfo Then Return
        ReadingDeviceInfo = True : BtnDeviceRefresh.IsEnabled = False
        Try
            TextDeviceInfo.Text = Await ReadDeviceInfo(Refresh)
        Catch
            TextDeviceInfo.Text = "暂时无法读取设备配置，请稍后刷新。"
        Finally
            ReadingDeviceInfo = False : BtnDeviceRefresh.IsEnabled = True
        End Try
    End Sub

    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub Jrrp()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub RubbishClear()
        Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        If ShowHint Then Hint("为便于维护，开源内容中不包含百宝箱功能……")
    End Sub
    Public Shared Function GetRandomCave() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return "为便于维护，开源内容中不包含百宝箱功能……"
    End Function

End Class
