Imports System.Windows.Media.Animation
Imports System.Windows.Threading

'页面准备时独立绘制胶囊；页面移动期间共用主绘制时钟。按钮与焦点始终由主界面线程管理。
Public Class MyNavigationIndicator
    Inherits FrameworkElement

    Private Host As HostVisual
    Private Session As IndicatorRenderer
    Private TargetX As Double, TargetWidth As Double
    Private Ink As DrawingGroup
    Private InkKey As String
    Private InkRetry As DispatcherOperation
    Private InkAttempts As Integer
    Private LocalRoot As ContainerVisual, LocalPill As DrawingVisual, LocalLabel As DrawingVisual
    Private LocalShift As TranslateTransform, LocalStretch As ScaleTransform
    Private LocalGlide As DoubleAnimation, LocalResize As DoubleAnimation
    Private PageMotionUsers As Integer, MotionGeneration As Integer
    Private MotionDeadline As Long
    Private LocalActive As Boolean
    Private ReturnTimer As DispatcherTimer
    Private CoordinatedMeasurement As IDisposable
    Public Shared ActiveRenderers As Integer

    Public Sub New()
        IsHitTestVisible = False
        SnapsToDevicePixels = False : UseLayoutRounding = False
    End Sub
    Protected Overrides ReadOnly Property VisualChildrenCount As Integer
        Get
            Return If(Host Is Nothing, 0, 2)
        End Get
    End Property
    Protected Overrides Function GetVisualChild(Index As Integer) As Visual
        If Host Is Nothing OrElse Index < 0 OrElse Index > 1 Then Throw New ArgumentOutOfRangeException(NameOf(Index))
        Return If(Index = 0, CType(Host, Visual), LocalRoot)
    End Function
    Private Sub StartRenderer() Handles Me.Loaded
        If Session IsNot Nothing Then Return
        Host = New HostVisual() : AddVisualChild(Host)
        LocalRoot = New ContainerVisual With {.Opacity = 0}
        LocalPill = New DrawingVisual() : LocalLabel = New DrawingVisual()
        LocalShift = New TranslateTransform() : LocalStretch = New ScaleTransform(1, 1)
        Dim Transform As New TransformGroup()
        Transform.Children.Add(LocalStretch) : Transform.Children.Add(LocalShift) : LocalPill.Transform = Transform
        LocalLabel.Clip = New RectangleGeometry(New Rect(0, 0, 78, 27), 13.5, 13.5) With {.Transform = Transform}
        Using Context = LocalPill.RenderOpen()
            Context.DrawRoundedRectangle(Brushes.White, Nothing, New Rect(0, 0, 78, 27), 13.5, 13.5)
        End Using
        LocalRoot.Children.Add(LocalPill) : LocalRoot.Children.Add(LocalLabel) : AddVisualChild(LocalRoot)
        DrawLocalInk()
        Session = New IndicatorRenderer(Host, Sub() Dispatcher.BeginInvoke(New Action(AddressOf InvalidateVisual)))
        Session.SetInk(Ink)
        Session.Request(TargetX, TargetWidth, False)
        Session.Start()
        InvalidateVisual()
    End Sub
    Private Sub StopRenderer() Handles Me.Unloaded
        MotionGeneration += 1 : PageMotionUsers = 0
        CancelReturnTimer() : LocalActive = False
        CoordinatedMeasurement?.Dispose() : CoordinatedMeasurement = Nothing
        ClearLocalAnimation()
        Dim Previous = Session : Session = Nothing
        Previous?.Shutdown()
        If InkRetry?.Status = DispatcherOperationStatus.Pending Then InkRetry.Abort()
        InkRetry = Nothing
        If Host IsNot Nothing Then RemoveVisualChild(Host) : Host = Nothing
        If LocalRoot IsNot Nothing Then RemoveVisualChild(LocalRoot)
        LocalRoot = Nothing : LocalPill = Nothing : LocalLabel = Nothing : LocalShift = Nothing : LocalStretch = Nothing
    End Sub

    Public Sub SelectTarget(X As Double, Width As Double, Animate As Boolean)
        If Double.IsNaN(X) OrElse Double.IsInfinity(X) OrElse Width <= 0 OrElse Double.IsNaN(Width) OrElse Double.IsInfinity(Width) Then Return
        If Math.Abs(TargetX - X) < 0.001 AndAlso Math.Abs(TargetWidth - Width) < 0.001 AndAlso NavigationMotionEnabled() Then Return
        TargetX = X : TargetWidth = Width
        MotionDeadline = GetTimeMs() + If(Animate AndAlso NavigationMotionEnabled(), 180, 0)
        Session?.Request(X, Width, Not LocalActive AndAlso Animate AndAlso NavigationMotionEnabled())
        If LocalActive Then
            AnimateLocal(LocalShift.X, LocalStretch.ScaleX * 78)
            If PageMotionUsers = 0 Then ScheduleReturn()
        End If
        If Session Is Nothing OrElse Session.Failed Then InvalidateVisual()
    End Sub
    Protected Overrides Sub OnRender(Context As DrawingContext)
        MyBase.OnRender(Context)
        If (Session Is Nothing OrElse Session.Failed) AndAlso TargetWidth > 0 AndAlso Ink IsNot Nothing Then
            Dim Clip As New RectangleGeometry(New Rect(TargetX, 0, TargetWidth, 27), 13.5, 13.5)
            Context.DrawGeometry(Brushes.White, Nothing, Clip)
            Context.PushClip(Clip) : Context.DrawDrawing(Ink) : Context.Pop()
        End If
    End Sub

    Friend Sub UpdateInk(CoordinateSpace As FrameworkElement, Selector As StackPanel)
        Dim Elements As New List(Of FrameworkElement)
        For Each Button In Selector.Children.OfType(Of MyRadioButton)().Where(Function(Value) Value.Visibility = Visibility.Visible)
            Elements.Add(Button.ShapeLogo) : Elements.Add(Button.LabText)
        Next
        Dim Brush = TryCast(CoordinateSpace.TryFindResource("ColorBrush3"), Brush)
        If Brush Is Nothing Then Brush = Brushes.DodgerBlue
        Brush = Brush.CloneCurrentValue() : Brush.Freeze()
        Dim Key = Brush.ToString() & String.Join("|", Elements.Select(Function(Item) Item.TranslatePoint(New Point(), CoordinateSpace).ToString() & ":" & Item.RenderSize.ToString() & If(TypeOf Item Is TextBlock, ":" & CType(Item, TextBlock).FontFamily.ToString() & ":" & CType(Item, TextBlock).Text, "")))
        If Key = InkKey Then Return
        Dim Drawing As New DrawingGroup()
        For Each Element In Elements
            Dim Content = VisualTreeHelper.GetDrawing(Element)
            If Content Is Nothing Then
                If InkAttempts < 3 AndAlso (InkRetry Is Nothing OrElse InkRetry.Status <> DispatcherOperationStatus.Pending) Then
                    InkAttempts += 1
                    InkRetry = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, New Action(
                        Sub()
                            InkRetry = Nothing
                            If IsLoaded Then UpdateInk(CoordinateSpace, Selector)
                        End Sub))
                End If
                Return
            End If
            Dim Copy = Content.CloneCurrentValue()
            Recolor(Copy, Brush)
            Dim Mapping = Element.TransformToAncestor(CoordinateSpace)
            Dim Origin = Mapping.Transform(New Point()), X = Mapping.Transform(New Point(1, 0)), Y = Mapping.Transform(New Point(0, 1))
            Dim Transform As New TransformGroup()
            If Copy.Transform IsNot Nothing Then Transform.Children.Add(Copy.Transform)
            Transform.Children.Add(New MatrixTransform(New Matrix(X.X - Origin.X, X.Y - Origin.Y, Y.X - Origin.X, Y.Y - Origin.Y, Origin.X, Origin.Y)))
            Copy.Transform = Transform : Drawing.Children.Add(Copy)
        Next
        Drawing.Freeze() : Ink = Drawing : InkKey = Key : InkAttempts = 0
        Session?.SetInk(Ink)
        DrawLocalInk()
        If Session Is Nothing OrElse Session.Failed Then InvalidateVisual()
    End Sub

    Private Sub DrawLocalInk()
        If LocalLabel Is Nothing Then Return
        Using Context = LocalLabel.RenderOpen()
            If Ink IsNot Nothing Then Context.DrawDrawing(Ink)
        End Using
    End Sub

    '页面开始移动后与胶囊共用主绘制时钟，避免两个 Dispatcher 同时提交而交替等待。
    '页面准备期间仍由独立绘制线程即时响应点击。
    Friend Function BeginPageMotion() As IDisposable
        If Session Is Nothing OrElse Session.Failed OrElse LocalRoot Is Nothing Then Return Nothing
        CancelReturnTimer()
        PageMotionUsers += 1
        If Not LocalActive Then
            LocalActive = True
            CoordinatedMeasurement = MeasureUiPhase("navigation.coordinated")
            Dim Position = Session.ReadSnapshotState()
            Host.Opacity = 0 : LocalRoot.Opacity = 1
            Session.Request(TargetX, TargetWidth, False)
            AnimateLocal(If(Position(1) > 0, Position(0), TargetX), If(Position(1) > 0, Position(1), TargetWidth))
        End If
        Return New PageMotionLease(Me, MotionGeneration)
    End Function
    Private Sub AnimateLocal(X As Double, Width As Double)
        ClearLocalAnimation()
        LocalShift.X = TargetX : LocalStretch.ScaleX = TargetWidth / 78
        Dim Remaining = Math.Max(0, Math.Min(180, MotionDeadline - GetTimeMs()))
        If Remaining < 1 OrElse Not NavigationMotionEnabled() Then Return
        LocalGlide = New DoubleAnimation(X, TargetX, TimeSpan.FromMilliseconds(Remaining)) With {.EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.Stop}
        LocalResize = New DoubleAnimation(Width / 78, TargetWidth / 78, TimeSpan.FromMilliseconds(Remaining)) With {.EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.Stop}
        LocalShift.BeginAnimation(TranslateTransform.XProperty, LocalGlide)
        LocalStretch.BeginAnimation(ScaleTransform.ScaleXProperty, LocalResize)
    End Sub
    Private Sub ClearLocalAnimation()
        LocalShift?.BeginAnimation(TranslateTransform.XProperty, Nothing)
        LocalStretch?.BeginAnimation(ScaleTransform.ScaleXProperty, Nothing)
        LocalGlide = Nothing : LocalResize = Nothing
    End Sub
    Private Sub EndPageMotion(Generation As Integer)
        If Generation <> MotionGeneration OrElse PageMotionUsers = 0 Then Return
        PageMotionUsers -= 1
        If PageMotionUsers <> 0 Then Return
        ScheduleReturn()
    End Sub
    Private Sub ScheduleReturn()
        CancelReturnTimer()
        Dim Remaining = MotionDeadline - GetTimeMs()
        If Remaining <= 0 OrElse Not NavigationMotionEnabled() Then ReturnToIndependent() : Return
        '快速切页中断内容过渡时，先让胶囊走完剩余路程，避免交回绘制权时跳到终点。
        ReturnTimer = New DispatcherTimer(DispatcherPriority.Background) With {.Interval = TimeSpan.FromMilliseconds(Remaining)}
        AddHandler ReturnTimer.Tick, AddressOf ReturnTick
        ReturnTimer.Start()
    End Sub
    Private Sub ReturnTick(Sender As Object, Args As EventArgs)
        CancelReturnTimer()
        If PageMotionUsers = 0 Then ReturnToIndependent()
    End Sub
    Private Sub CancelReturnTimer()
        If ReturnTimer Is Nothing Then Return
        ReturnTimer.Stop() : RemoveHandler ReturnTimer.Tick, AddressOf ReturnTick : ReturnTimer = Nothing
    End Sub
    Private Sub ReturnToIndependent()
        LocalActive = False
        CoordinatedMeasurement?.Dispose() : CoordinatedMeasurement = Nothing
        ClearLocalAnimation()
        If LocalRoot IsNot Nothing Then LocalRoot.Opacity = 0
        If Host IsNot Nothing Then Host.Opacity = 1
        Session?.Request(TargetX, TargetWidth, False)
    End Sub
    Private NotInheritable Class PageMotionLease
        Implements IDisposable
        Private Owner As MyNavigationIndicator
        Private ReadOnly Generation As Integer
        Public Sub New(Indicator As MyNavigationIndicator, Value As Integer)
            Owner = Indicator : Generation = Value
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            Dim Previous = Owner : Owner = Nothing
            Previous?.EndPageMotion(Generation)
        End Sub
    End Class
    Private Shared Sub Recolor(Drawing As Drawing, Brush As Brush)
        If TypeOf Drawing Is DrawingGroup Then
            For Each Child In CType(Drawing, DrawingGroup).Children
                Recolor(Child, Brush)
            Next
        ElseIf TypeOf Drawing Is GeometryDrawing Then
            CType(Drawing, GeometryDrawing).Brush = Brush
        ElseIf TypeOf Drawing Is GlyphRunDrawing Then
            CType(Drawing, GlyphRunDrawing).ForegroundBrush = Brush
        End If
    End Sub

    '复核时异步取快照，主界面不得同步等待绘制线程。
    Friend Function ReadMotionStateAsync() As Task(Of Double())
        If LocalActive Then Return Task.FromResult({LocalShift.X, LocalStretch.ScaleX * 78, If(LocalShift.HasAnimatedProperties OrElse LocalStretch.HasAnimatedProperties, 1.0, 0.0)})
        If Session Is Nothing Then Return Task.FromResult({TargetX, TargetWidth, 0.0})
        Return Session.ReadStateAsync()
    End Function

    'RenderTargetBitmap 不会跨线程读取 HostVisual，快照需补绘同一份选中内容。
    Friend Function GetSnapshotDrawing() As DrawingGroup
        '协同绘制时已在主视觉树内，RenderTargetBitmap 会直接捕获，不能叠绘两次。
        If LocalActive Then Return Nothing
        If Ink Is Nothing OrElse TargetWidth <= 0 OrElse Not IsVisible Then Return Nothing
        Dim Position = If(Session Is Nothing, New Double() {TargetX, TargetWidth}, Session.ReadSnapshotState())
        Dim Drawing As New DrawingGroup()
        Using Context = Drawing.Open()
            Dim Clip As New RectangleGeometry(New Rect(Position(0), 0, Position(1), 27), 13.5, 13.5)
            Context.DrawGeometry(Brushes.White, Nothing, Clip)
            Context.PushClip(Clip) : Context.DrawDrawing(Ink) : Context.Pop()
        End Using
        Drawing.Freeze() : Return Drawing
    End Function

    Private Class IndicatorRenderer
        Private ReadOnly Gate As New Object()
        Private ReadOnly Host As HostVisual
        Private FailedCallback As Action
        Private Worker As Thread, Queue As Dispatcher
        Private Stopping As Boolean, Pending As Boolean, IsFailed As Boolean
        Private RequestedX As Double, RequestedWidth As Double, RequestedAnimation As Boolean
        Private Root As ContainerVisual, Pill As DrawingVisual, Label As DrawingVisual
        Private Shift As TranslateTransform, Stretch As ScaleTransform
        Private RequestedInk As DrawingGroup, InkVersion As Integer, AppliedInkVersion As Integer = -1
        Private PositionVersion As Integer, AppliedPositionVersion As Integer = -1
        Private Glide As DoubleAnimation, Resize As DoubleAnimation
        Private Initialized As Boolean
        Private PublishedPosition As Double() = {0.0, 0.0}

        Public Sub New(Parent As HostVisual, Failure As Action)
            Host = Parent : FailedCallback = Failure
        End Sub
        Public ReadOnly Property Failed As Boolean
            Get
                SyncLock Gate
                    Return IsFailed
                End SyncLock
            End Get
        End Property
        Public Sub Start()
            Worker = New Thread(AddressOf Run) With {.IsBackground = True, .Name = "PCL Navigation Drawing", .Priority = ThreadPriority.Normal}
            Worker.SetApartmentState(ApartmentState.STA) : Worker.Start()
        End Sub
        Private Sub Run()
            Interlocked.Increment(ActiveRenderers)
            Dim Target As VisualTarget = Nothing
            Try
                Dim Current = Dispatcher.CurrentDispatcher
                Root = New ContainerVisual()
                Pill = New DrawingVisual() : Label = New DrawingVisual()
                Shift = New TranslateTransform() : Stretch = New ScaleTransform(1, 1)
                Dim Transform As New TransformGroup()
                Transform.Children.Add(Stretch) : Transform.Children.Add(Shift) : Pill.Transform = Transform
                Label.Clip = New RectangleGeometry(New Rect(0, 0, 78, 27), 13.5, 13.5) With {.Transform = Transform}
                Using Context = Pill.RenderOpen()
                    Context.DrawRoundedRectangle(Brushes.White, Nothing, New Rect(0, 0, 78, 27), 13.5, 13.5)
                End Using
                Root.Children.Add(Pill) : Root.Children.Add(Label)
                Target = New VisualTarget(Host) With {.RootVisual = Root}
                SyncLock Gate
                    If Stopping Then Return
                    Queue = Current
                End SyncLock
                ApplyLatest()
                Dispatcher.Run()
            Catch Ex As Exception
                Logger.Warn(Ex, "独立导航绘制不可用，使用静态选中样式")
                Dim Notify As Action
                SyncLock Gate
                    IsFailed = True : Notify = If(Stopping, Nothing, FailedCallback)
                End SyncLock
                Notify?.Invoke()
            Finally
                ClearAnimation()
                Target?.Dispose()
                Root = Nothing : Pill = Nothing : Label = Nothing : Shift = Nothing : Stretch = Nothing
                SyncLock Gate
                    Queue = Nothing : FailedCallback = Nothing
                End SyncLock
                Interlocked.Decrement(ActiveRenderers)
            End Try
        End Sub

        Public Sub Request(X As Double, Width As Double, Animate As Boolean)
            SyncLock Gate
                If Stopping Then Return
                RequestedX = X : RequestedWidth = Width : RequestedAnimation = Animate
                PositionVersion += 1
                If Queue Is Nothing OrElse Pending Then Return
                Pending = True
                Queue.BeginInvoke(DispatcherPriority.Render, New Action(AddressOf ApplyLatest))
            End SyncLock
        End Sub
        Public Sub SetInk(Content As DrawingGroup)
            SyncLock Gate
                If Stopping Then Return
                RequestedInk = Content : InkVersion += 1
                If Queue Is Nothing OrElse Pending Then Return
                Pending = True
                Queue.BeginInvoke(DispatcherPriority.Render, New Action(AddressOf ApplyLatest))
            End SyncLock
        End Sub
        Private Sub ApplyLatest()
            Dim X As Double, Width As Double, Animate As Boolean, Content As DrawingGroup, ContentVersion As Integer, PositionSequence As Integer
            SyncLock Gate
                Pending = False
                If Stopping Then Return
                X = RequestedX : Width = RequestedWidth : Animate = RequestedAnimation
                Content = RequestedInk : ContentVersion = InkVersion : PositionSequence = PositionVersion
            End SyncLock
            If ContentVersion <> AppliedInkVersion Then
                Using Context = Label.RenderOpen()
                    If Content IsNot Nothing Then Context.DrawDrawing(Content)
                End Using
                Root.Opacity = If(Content Is Nothing, 0, 1)
                AppliedInkVersion = ContentVersion
            End If
            If PositionSequence = AppliedPositionVersion Then Return
            AppliedPositionVersion = PositionSequence
            Dim PreviousX = Shift.X, PreviousWidth = Stretch.ScaleX * 78
            ClearAnimation()
            Shift.X = X : Stretch.ScaleX = Width / 78
            PublishPosition(Nothing, EventArgs.Empty)
            If Not Initialized OrElse Not Animate OrElse Math.Abs(PreviousX - X) < 0.01 Then Initialized = Width > 0 : Return
            Glide = New DoubleAnimation(PreviousX, X, TimeSpan.FromMilliseconds(180)) With {.EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.Stop}
            Resize = New DoubleAnimation(PreviousWidth / 78, Width / 78, TimeSpan.FromMilliseconds(180)) With {.EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.Stop}
            AddHandler Glide.CurrentTimeInvalidated, AddressOf PublishPosition
            AddHandler Glide.Completed, AddressOf Completed
            Shift.BeginAnimation(TranslateTransform.XProperty, Glide, HandoffBehavior.SnapshotAndReplace)
            Stretch.BeginAnimation(ScaleTransform.ScaleXProperty, Resize, HandoffBehavior.SnapshotAndReplace)
        End Sub
        Private Sub ClearAnimation()
            If Glide IsNot Nothing Then
                RemoveHandler Glide.Completed, AddressOf Completed
                RemoveHandler Glide.CurrentTimeInvalidated, AddressOf PublishPosition
            End If
            Shift?.BeginAnimation(TranslateTransform.XProperty, Nothing)
            Stretch?.BeginAnimation(ScaleTransform.ScaleXProperty, Nothing)
            Glide = Nothing : Resize = Nothing
        End Sub
        Private Sub PublishPosition(Sender As Object, E As EventArgs)
            If Shift IsNot Nothing Then Threading.Volatile.Write(PublishedPosition, New Double() {Shift.X, Stretch.ScaleX * 78})
        End Sub
        Public Function ReadSnapshotState() As Double()
            Return Threading.Volatile.Read(PublishedPosition)
        End Function
        Private Sub Completed(Sender As Object, E As EventArgs)
            ClearAnimation()
            PublishPosition(Nothing, EventArgs.Empty)
        End Sub
        Public Sub Shutdown()
            SyncLock Gate
                If Stopping Then Return
                Stopping = True : FailedCallback = Nothing
                Queue?.BeginInvokeShutdown(DispatcherPriority.Send)
            End SyncLock
        End Sub
        Public Function ReadStateAsync() As Task(Of Double())
            SyncLock Gate
                If Queue Is Nothing OrElse Stopping Then Return Task.FromResult({RequestedX, RequestedWidth, 0.0})
                Return Queue.InvokeAsync(Function() New Double() {Shift.X, Stretch.ScaleX * 78, If(Shift.HasAnimatedProperties OrElse Stretch.HasAnimatedProperties, 1.0, 0.0)}, DispatcherPriority.Normal).Task
            End SyncLock
        End Function
    End Class
End Class
