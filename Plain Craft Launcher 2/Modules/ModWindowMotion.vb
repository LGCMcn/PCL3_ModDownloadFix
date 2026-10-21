Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Windows.Media.Animation
Imports System.Windows.Media.Imaging
Imports System.Windows.Media.Media3D

'短时窗口效果。快照只存在于一次最小化期间，不订阅全局逐帧事件。
Friend Class WindowMotion
    Implements IDisposable
    Private ReadOnly Owner As Window
    Private Overlay As Window
    Private Surface As TaskbarWarpSurface
    Private CloseFade As DoubleAnimation
    Private CloseScale As ScaleTransform
    Private CloseCallback As Action
    Private CloseRoot As FrameworkElement
    Private PreviousTransform As Transform
    Private PreviousOpacity As Double
    Private NativeSource As HwndSource
    Private IsClosing As Boolean
    Private IsPreparing As Boolean
    Private PreparationGeneration As Integer
    Private IsDisposed As Boolean
    Public ReadOnly Property IsMinimizing As Boolean
        Get
            Return Overlay IsNot Nothing OrElse IsPreparing
        End Get
    End Property

    Public Sub New(Target As Window)
        Owner = Target
        AddHandler Owner.Closed, AddressOf OnClosed
        AddHandler Owner.StateChanged, AddressOf OnStateChanged
        AddHandler Owner.Activated, AddressOf WarmTaskbarTarget
        NativeSource = TryCast(PresentationSource.FromVisual(Target), HwndSource)
        NativeSource?.AddHook(AddressOf WindowMessage)
        WarmTaskbarTarget(Nothing, EventArgs.Empty)
    End Sub

    Private Async Sub WarmTaskbarTarget(Sender As Object, E As EventArgs)
        If IsDisposed OrElse Not Owner.ShowInTaskbar Then Return
        Dim Handle = New WindowInteropHelper(Owner).Handle
        If Handle <> IntPtr.Zero Then Await LocateTaskbarButton(GetScreenBounds(Handle))
    End Sub

    Private Function WindowMessage(Hwnd As IntPtr, Message As Integer, WParam As IntPtr, LParam As IntPtr, ByRef Handled As Boolean) As IntPtr
        If Message = &H112 AndAlso (WParam.ToInt64() And &HFFF0L) = &HF020 Then
            Handled = True : Minimize()
        End If
        Return IntPtr.Zero
    End Function

    Public Async Sub Minimize()
        If IsDisposed OrElse IsClosing OrElse IsMinimizing OrElse Owner.WindowState = WindowState.Minimized Then Return
        If Not Settings.Get(Of Boolean)("UiMinimizeAnimation") OrElse Not MotionAvailable() OrElse (RenderCapability.Tier >> 16) < 2 OrElse SystemParameters.IsRemoteSession OrElse AniIsRun("Form Show") Then
            Owner.WindowState = WindowState.Minimized : Return
        End If
        Try
            Dim Handle = New WindowInteropHelper(Owner).Handle
            Dim Bounds As NativeRect
            If Not GetWindowRect(Handle, Bounds) Then Throw New InvalidOperationException("无法读取窗口边界。")
            Dim Scale = PresentationSource.FromVisual(Owner).CompositionTarget.TransformToDevice
            Dim SourceBounds As New Rect(Bounds.Left, Bounds.Top, Bounds.Right - Bounds.Left, Bounds.Bottom - Bounds.Top)
            Dim Edge As Integer
            Dim Destination = FindTaskbarTarget(Handle, SourceBounds, Edge)
            IsPreparing = True
            Dim Generation = PreparationGeneration
            Dim Button As Rect?
            Using MeasureUiPhase("minimize.target")
                Button = Await LocateTaskbarButton(GetScreenBounds(Handle))
            End Using
            If Generation <> PreparationGeneration OrElse IsClosing OrElse IsDisposed OrElse Owner.WindowState = WindowState.Minimized Then Return
            IsPreparing = False
            If Button.HasValue Then Destination = TaskbarButtonEndpoint(Button.Value)
            Dim Area = Rect.Union(SourceBounds, Destination) : Area.Inflate(2, 2)
            Dim Quality = Math.Min(Scale.M11, Math.Min(1600 / Owner.ActualWidth, 1000 / Owner.ActualHeight))
            Dim Snapshot As RenderTargetBitmap = Nothing
            Using MeasureUiPhase("minimize.snapshot")
                Dim Main = TryCast(Owner, FormMain)
                Dim ScreenImage As BitmapSource = Nothing
                If Main IsNot Nothing Then
                    Using MeasureUiPhase("minimize.capture")
                        ScreenImage = CaptureVisibleWindow(Handle, SourceBounds, Scale.M11)
                    End Using
                End If
                If ScreenImage IsNot Nothing Then
                    Snapshot = New RenderTargetBitmap(Math.Max(1, CInt(Owner.ActualWidth * Quality)), Math.Max(1, CInt(Owner.ActualHeight * Quality)), 96 * Quality, 96 * Quality, PixelFormats.Pbgra32)
                    Dim Visual As New DrawingVisual()
                    Dim Origin = Main.BorderForm.TranslatePoint(New Point(), Owner)
                    Using Context = Visual.RenderOpen()
                        Context.PushClip(New RectangleGeometry(New Rect(Origin, Main.BorderForm.RenderSize), 6, 6))
                        Context.DrawImage(ScreenImage, New Rect(0, 0, Owner.ActualWidth, Owner.ActualHeight))
                        Context.Pop()
                    End Using
                    Snapshot.Render(Visual) : Snapshot.Freeze()
                Else
                    Snapshot = New RenderTargetBitmap(Math.Max(1, CInt(Owner.ActualWidth * Quality)), Math.Max(1, CInt(Owner.ActualHeight * Quality)), 96 * Quality, 96 * Quality, PixelFormats.Pbgra32)
                    Snapshot.Render(Owner)
                    Dim Header = TryCast(Owner, FormMain)?.TitleSelection
                    Dim Selection = Header?.GetSnapshotDrawing()
                    If Selection IsNot Nothing Then
                        Dim Position = Header.TranslatePoint(New Point(), Owner)
                        Dim Patch As New DrawingVisual()
                        Using Context = Patch.RenderOpen()
                            Context.PushTransform(New TranslateTransform(Position.X, Position.Y))
                            Context.DrawDrawing(Selection) : Context.Pop()
                        End Using
                        Snapshot.Render(Patch)
                    End If
                    Snapshot.Freeze()
                End If
            End Using
            Dim LocalSource As New Rect((SourceBounds.X - Area.X) / Scale.M11, (SourceBounds.Y - Area.Y) / Scale.M22, SourceBounds.Width / Scale.M11, SourceBounds.Height / Scale.M22)
            Dim LocalTarget As New Rect((Destination.X - Area.X) / Scale.M11, (Destination.Y - Area.Y) / Scale.M22, Destination.Width / Scale.M11, Destination.Height / Scale.M22)
            Surface = New TaskbarWarpSurface(Snapshot, LocalSource, LocalTarget, Edge, Area.Width / Scale.M11, Area.Height / Scale.M22)
            Overlay = New Window With {.WindowStyle = WindowStyle.None, .AllowsTransparency = True, .Background = Brushes.Transparent,
                .ShowInTaskbar = False, .ShowActivated = False, .ResizeMode = ResizeMode.NoResize, .IsHitTestVisible = False,
                .Width = Area.Width / Scale.M11, .Height = Area.Height / Scale.M22, .Left = Owner.Left, .Top = Owner.Top, .Content = Surface}
            AddHandler Overlay.SourceInitialized, Sub()
                Dim OverlayHandle = New WindowInteropHelper(Overlay).Handle
                Dim Flags = GetWindowLong(OverlayHandle, -20)
                SetWindowLong(OverlayHandle, -20, Flags Or &H8000000 Or &H20 Or &H80) '不激活、鼠标穿透、工具窗口
            End Sub
            PreviousOpacity = Owner.Opacity
            Using MeasureUiPhase("minimize.overlay-show")
                Overlay.Show()
            End Using
            SetWindowPos(New WindowInteropHelper(Overlay).Handle, New IntPtr(-1), CInt(Area.X), CInt(Area.Y), CInt(Area.Width), CInt(Area.Height), &H10)
            Owner.Opacity = 0
            Surface.Play(AddressOf FinishMinimize)
        Catch Ex As Exception
            CancelMinimize()
            Logger.Warn(Ex, "窗口收拢效果不可用，直接最小化")
            Owner.WindowState = WindowState.Minimized
        End Try
    End Sub

    Private Sub FinishMinimize()
        '先设置真实最小化状态，再释放覆盖层；StateChanged 也会执行同一清理。
        Owner.WindowState = WindowState.Minimized
        CancelMinimize()
    End Sub

    Public Sub CancelMinimize()
        PreparationGeneration += 1 : IsPreparing = False
        Dim Old = Overlay : Overlay = Nothing
        Surface?.Release() : Surface = Nothing
        If Old Is Nothing Then Return
        Old.Content = Nothing : Old.Close()
        Owner.Opacity = PreviousOpacity
    End Sub

    Public Sub PlayClose(Root As FrameworkElement, Finished As Action)
        If IsClosing OrElse IsDisposed Then Return
        CancelMinimize() : IsClosing = True
        If Not Settings.Get(Of Boolean)("UiCloseAnimation") OrElse Not MotionAvailable() OrElse Not Owner.IsVisible Then Finished() : Return
        CloseRoot = Root : PreviousTransform = Root.RenderTransform : PreviousOpacity = Owner.Opacity
        CloseScale = New ScaleTransform(1, 1)
        Dim Group As New TransformGroup()
        If PreviousTransform IsNot Nothing Then Group.Children.Add(PreviousTransform)
        Group.Children.Add(CloseScale) : Root.RenderTransform = Group
        CloseCallback = Finished
        CloseFade = New DoubleAnimation(PreviousOpacity, 0, TimeSpan.FromMilliseconds(150)) With {.EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
        Dim Shrink As New DoubleAnimation(1, 0.94, TimeSpan.FromMilliseconds(150)) With {.EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}}
        AddHandler CloseFade.Completed, AddressOf OnCloseFinished
        CloseScale.BeginAnimation(ScaleTransform.ScaleXProperty, Shrink, HandoffBehavior.SnapshotAndReplace)
        CloseScale.BeginAnimation(ScaleTransform.ScaleYProperty, Shrink, HandoffBehavior.SnapshotAndReplace)
        Owner.BeginAnimation(UIElement.OpacityProperty, CloseFade, HandoffBehavior.SnapshotAndReplace)
    End Sub
    Private Sub OnCloseFinished(Sender As Object, E As EventArgs)
        RemoveHandler CloseFade.Completed, AddressOf OnCloseFinished
        Owner.Opacity = 0 : CloseScale.ScaleX = 0.94 : CloseScale.ScaleY = 0.94
        Owner.BeginAnimation(UIElement.OpacityProperty, Nothing)
        CloseScale.BeginAnimation(ScaleTransform.ScaleXProperty, Nothing)
        CloseScale.BeginAnimation(ScaleTransform.ScaleYProperty, Nothing)
        CloseFade = Nothing
        Dim Callback = CloseCallback : CloseCallback = Nothing
        Callback?.Invoke()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If IsDisposed Then Return
        IsDisposed = True : CancelMinimize()
        If CloseFade IsNot Nothing Then RemoveHandler CloseFade.Completed, AddressOf OnCloseFinished
        CloseFade = Nothing : CloseCallback = Nothing
        If CloseRoot IsNot Nothing Then Owner.BeginAnimation(UIElement.OpacityProperty, Nothing)
        CloseScale?.BeginAnimation(ScaleTransform.ScaleXProperty, Nothing)
        CloseScale?.BeginAnimation(ScaleTransform.ScaleYProperty, Nothing)
        CloseScale = Nothing
        If CloseRoot IsNot Nothing Then CloseRoot.RenderTransform = PreviousTransform : Owner.Opacity = PreviousOpacity
        CloseRoot = Nothing : PreviousTransform = Nothing
        RemoveHandler Owner.Closed, AddressOf OnClosed
        RemoveHandler Owner.StateChanged, AddressOf OnStateChanged
        RemoveHandler Owner.Activated, AddressOf WarmTaskbarTarget
        NativeSource?.RemoveHook(AddressOf WindowMessage) : NativeSource = Nothing
    End Sub
    Private Sub OnClosed(Sender As Object, E As EventArgs)
        Dispose()
    End Sub
    Private Sub OnStateChanged(Sender As Object, E As EventArgs)
        If IsMinimizing Then CancelMinimize()
    End Sub

    Friend Shared Function ChooseTaskbarTarget(Screen As Rect, Work As Rect, Source As Rect, ByRef Edge As Integer) As Rect
        '副屏、纵向任务栏使用所在屏幕的工作区。自动隐藏且无保留区时退回屏幕底边。
        Dim Insets = {Math.Max(0, Work.Left - Screen.Left), Math.Max(0, Work.Top - Screen.Top), Math.Max(0, Screen.Right - Work.Right), Math.Max(0, Screen.Bottom - Work.Bottom)}
        Edge = Array.IndexOf(Insets, Insets.Max())
        If Insets.Max() < 2 Then Edge = 3
        Dim X = Math.Max(Screen.Left + 30, Math.Min(Screen.Right - 30, Source.Left + Source.Width / 2))
        Dim Y = Math.Max(Screen.Top + 30, Math.Min(Screen.Bottom - 30, Source.Top + Source.Height / 2))
        Select Case Edge
            Case 0 : Return New Rect(Screen.Left + Math.Max(12, Insets(0) / 2) - 12, Y - 12, 24, 24)
            Case 1 : Return New Rect(X - 12, Screen.Top + Math.Max(12, Insets(1) / 2) - 12, 24, 24)
            Case 2 : Return New Rect(Screen.Right - Math.Max(12, Insets(2) / 2) - 12, Y - 12, 24, 24)
            Case Else : Return New Rect(X - 12, Screen.Bottom - Math.Max(12, Insets(3) / 2) - 12, 24, 24)
        End Select
    End Function
    Private Shared Function GetScreenBounds(Handle As IntPtr) As Rect
        Dim Info As New MonitorInfo With {.Size = Marshal.SizeOf(GetType(MonitorInfo))}
        If Not GetMonitorInfo(MonitorFromWindow(Handle, 2), Info) Then Return Rect.Empty
        Return New Rect(Info.Monitor.Left, Info.Monitor.Top, Info.Monitor.Right - Info.Monitor.Left, Info.Monitor.Bottom - Info.Monitor.Top)
    End Function
    Private Shared Function FindTaskbarTarget(Handle As IntPtr, Source As Rect, ByRef Edge As Integer) As Rect
        Dim Info As New MonitorInfo With {.Size = Marshal.SizeOf(GetType(MonitorInfo))}
        If Not GetMonitorInfo(MonitorFromWindow(Handle, 2), Info) Then Throw New InvalidOperationException("无法读取所在屏幕。")
        Dim Target = ChooseTaskbarTarget(New Rect(Info.Monitor.Left, Info.Monitor.Top, Info.Monitor.Right - Info.Monitor.Left, Info.Monitor.Bottom - Info.Monitor.Top),
            New Rect(Info.Work.Left, Info.Work.Top, Info.Work.Right - Info.Work.Left, Info.Work.Bottom - Info.Work.Top), Source, Edge)
        Dim Side = 24 * WindowDpiScale(Handle)
        Dim X = Math.Max(Info.Monitor.Left, Math.Min(Info.Monitor.Right - Side, Target.X + Target.Width / 2 - Side / 2))
        Dim Y = Math.Max(Info.Monitor.Top, Math.Min(Info.Monitor.Bottom - Side, Target.Y + Target.Height / 2 - Side / 2))
        Return New Rect(X, Y, Side, Side)
    End Function
    <StructLayout(LayoutKind.Sequential)> Private Structure NativeRect
        Public Left, Top, Right, Bottom As Integer
    End Structure
    <StructLayout(LayoutKind.Sequential)> Private Structure MonitorInfo
        Public Size As Integer
        Public Monitor, Work As NativeRect
        Public Flags As Integer
    End Structure
    <DllImport("user32.dll")> Private Shared Function GetWindowRect(Handle As IntPtr, ByRef Bounds As NativeRect) As Boolean
    End Function
    <DllImport("user32.dll")> Private Shared Function MonitorFromWindow(Handle As IntPtr, Flags As Integer) As IntPtr
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Auto)> Private Shared Function GetMonitorInfo(Handle As IntPtr, ByRef Info As MonitorInfo) As Boolean
    End Function
    <DllImport("user32.dll", EntryPoint:="GetWindowLongW")> Private Shared Function GetWindowLong(Handle As IntPtr, Index As Integer) As Integer
    End Function
    <DllImport("user32.dll", EntryPoint:="SetWindowLongW")> Private Shared Function SetWindowLong(Handle As IntPtr, Index As Integer, Value As Integer) As Integer
    End Function
    <DllImport("user32.dll")> Private Shared Function SetWindowPos(Handle As IntPtr, InsertAfter As IntPtr, X As Integer, Y As Integer, Width As Integer, Height As Integer, Flags As Integer) As Boolean
    End Function
End Class

Friend Class TaskbarWarpSurface
    Inherits Grid
    Private ReadOnly SourceBounds As Rect, TargetBounds As Rect, Edge As Integer
    Private ReadOnly Mesh As New MeshGeometry3D()
    Private ReadOnly Texture As ImageBrush
    Private ReadOnly View As New Viewport3D()
    Private ReadOnly Rows As Integer = 32, Columns As Integer = 8
    Private Completion As Action
    Private Motion As DoubleAnimation
    Public Shared ReadOnly ProgressProperty As DependencyProperty = DependencyProperty.Register("Progress", GetType(Double), GetType(TaskbarWarpSurface), New PropertyMetadata(0.0, Sub(D, E) CType(D, TaskbarWarpSurface).UpdateMesh(CDbl(E.NewValue))))
    Public Sub New(Image As BitmapSource, Source As Rect, Target As Rect, TaskbarEdge As Integer, AreaWidth As Double, AreaHeight As Double)
        SourceBounds = Source : TargetBounds = Target : Edge = TaskbarEdge
        Width = AreaWidth : Height = AreaHeight : IsHitTestVisible = False
        Texture = New ImageBrush(Image) With {.Stretch = Stretch.Fill}
        Dim Coordinates As New PointCollection(), Indices As New Int32Collection()
        For Row = 0 To Rows
            For Column = 0 To Columns
                Coordinates.Add(New Point(Column / Columns, Row / Rows))
                If Row = Rows OrElse Column = Columns Then Continue For
                Dim I = Row * (Columns + 1) + Column
                For Each N In {I, I + Columns + 1, I + 1, I + 1, I + Columns + 1, I + Columns + 2}
                    Indices.Add(N)
                Next
            Next
        Next
        Coordinates.Freeze() : Indices.Freeze() : Mesh.TextureCoordinates = Coordinates : Mesh.TriangleIndices = Indices
        Dim Material As New DiffuseMaterial(Texture)
        View.Children.Add(New ModelVisual3D With {.Content = New AmbientLight(Colors.White)})
        View.Camera = New OrthographicCamera(New Point3D(AreaWidth / 2, -AreaHeight / 2, 1000), New Vector3D(0, 0, -1), New Vector3D(0, 1, 0), AreaWidth)
        View.Children.Add(New ModelVisual3D With {.Content = New GeometryModel3D(Mesh, Material) With {.BackMaterial = Material}})
        Children.Add(View) : UpdateMesh(0)
    End Sub
    Private Shared Function Smooth(Value As Double) As Double
        Value = Math.Max(0, Math.Min(1, Value)) : Return Value * Value * (3 - 2 * Value)
    End Function
    Private Sub UpdateMesh(Progress As Double)
        Dim Points As New Point3DCollection((Rows + 1) * (Columns + 1))
        Dim Travel = 1 - (1 - Math.Max(0, Math.Min(1, Progress / 0.82))) ^ 2
        For Row = 0 To Rows
            For Column = 0 To Columns
                Dim U = Column / Columns, V = Row / Rows
                Dim Near = If(Edge = 0, 1 - U, If(Edge = 1, 1 - V, If(Edge = 2, U, V)))
                Dim Pinch = Smooth((Progress - 0.28 * (1 - Near)) / 0.52)
                Dim XBlend = If(Edge = 0 OrElse Edge = 2, Travel, Pinch)
                Dim YBlend = If(Edge = 0 OrElse Edge = 2, Pinch, Travel)
                Dim X = (SourceBounds.X + U * SourceBounds.Width) * (1 - XBlend) + (TargetBounds.X + U * TargetBounds.Width) * XBlend
                Dim Y = (SourceBounds.Y + V * SourceBounds.Height) * (1 - YBlend) + (TargetBounds.Y + V * TargetBounds.Height) * YBlend
                Points.Add(New Point3D(X, -Y, 0))
            Next
        Next
        Mesh.Positions = Points
        Opacity = 1 - Smooth((Progress - 0.82) / 0.18)
    End Sub
    Public Sub Play(Finished As Action)
        Completion = Finished
        Motion = New DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) With {.FillBehavior = FillBehavior.HoldEnd}
        AddHandler Motion.Completed, AddressOf OnFinished
        BeginAnimation(ProgressProperty, Motion, HandoffBehavior.SnapshotAndReplace)
    End Sub
    Private Sub OnFinished(Sender As Object, E As EventArgs)
        Dim Callback = Completion : Completion = Nothing
        Callback?.Invoke()
    End Sub
    Public Sub Release()
        If Motion IsNot Nothing Then RemoveHandler Motion.Completed, AddressOf OnFinished
        BeginAnimation(ProgressProperty, Nothing)
        Motion = Nothing : Completion = Nothing
        Texture.ImageSource = Nothing : View.Children.Clear() : Children.Clear()
    End Sub
End Class
