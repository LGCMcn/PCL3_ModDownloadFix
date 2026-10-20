Imports System.Runtime.InteropServices
Imports System.Windows.Automation

Friend Module ModTaskbarTarget
    Public Const TaskbarApplicationId As String = "LGCMcn.PCL.DownloadFix"
    Private ReadOnly QueryLock As New Object()
    Private RunningQuery As Task(Of Rect?)
    Private CachedButton As Rect?
    Private CachedAt As DateTime

    Public Sub InitializeTaskbarIdentity()
        Try
            SetCurrentProcessExplicitAppUserModelID(TaskbarApplicationId)
        Catch
            '替代桌面环境可能没有该 Shell 接口，定位失败时使用屏幕边缘。
        End Try
    End Sub

    Public Async Function LocateTaskbarButton(Screen As Rect) As Task(Of Rect?)
        Dim Query As Task(Of Rect?)
        SyncLock QueryLock
            If RunningQuery Is Nothing OrElse RunningQuery.IsCompleted Then
                RunningQuery = Task.Run(Function() QueryTaskbar(Screen))
            End If
            Query = RunningQuery
        End SyncLock
        'UIA 提供方可能挂起。单一任务防止重复堆积，等待不阻断界面线程。
        If Await Task.WhenAny(Query, Task.Delay(80)).ConfigureAwait(False) Is Query Then
            Dim Found = Await Query.ConfigureAwait(False)
            SyncLock QueryLock
                CachedButton = Found : CachedAt = DateTime.UtcNow
            End SyncLock
            Return Found
        End If
        SyncLock QueryLock
            If CachedButton.HasValue AndAlso Screen.IntersectsWith(CachedButton.Value) AndAlso DateTime.UtcNow - CachedAt < TimeSpan.FromSeconds(10) Then Return CachedButton
        End SyncLock
        Return Nothing
    End Function

    Private Function QueryTaskbar(Screen As Rect) As Rect?
        Try
            Dim Bars As New List(Of IntPtr) From {FindWindow("Shell_TrayWnd", Nothing)}
            Dim Previous = IntPtr.Zero
            Do
                Previous = FindWindowEx(IntPtr.Zero, Previous, "Shell_SecondaryTrayWnd", Nothing)
                If Previous = IntPtr.Zero Then Exit Do
                Bars.Add(Previous)
            Loop
            Dim Result As Rect? = Nothing, Distance = Double.MaxValue
            Dim Condition As New PropertyCondition(AutomationElement.AutomationIdProperty, "Appid: " & TaskbarApplicationId)
            For Each Handle In Bars.Where(Function(Value) Value <> IntPtr.Zero)
                Dim Cache As New CacheRequest()
                    Cache.Add(AutomationElement.BoundingRectangleProperty)
                    Cache.Add(AutomationElement.IsOffscreenProperty)
                    Using Cache.Activate()
                        Dim Root = AutomationElement.FromHandle(Handle)
                        For Each Element As AutomationElement In Root.FindAll(TreeScope.Descendants, Condition)
                            Dim Bounds = Element.Cached.BoundingRectangle
                            If Element.Cached.IsOffscreen OrElse Bounds.IsEmpty OrElse Bounds.Width <= 0 OrElse Bounds.Height <= 0 Then Continue For
                            '多屏分别显示按钮时，优先选择窗口所在的屏幕。
                            Dim Score = If(Screen.IntersectsWith(Bounds), 0.0, 1000000.0) + Math.Abs(Bounds.X + Bounds.Width / 2 - Screen.X - Screen.Width / 2)
                            If Score < Distance Then Distance = Score : Result = Bounds
                        Next
                    End Using
            Next
            Return Result
        Catch
            Return Nothing
        End Try
    End Function

    Public Function TaskbarButtonEndpoint(Button As Rect) As Rect
        Return New Rect(Button.X + Button.Width / 2 - 2, Button.Y + Button.Height / 2 - 2, 4, 4)
    End Function
    <DllImport("shell32.dll", CharSet:=CharSet.Unicode)> Private Function SetCurrentProcessExplicitAppUserModelID(Value As String) As Integer
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Unicode)> Private Function FindWindow(ClassName As String, Title As String) As IntPtr
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Unicode)> Private Function FindWindowEx(Parent As IntPtr, After As IntPtr, ClassName As String, Title As String) As IntPtr
    End Function
End Module
