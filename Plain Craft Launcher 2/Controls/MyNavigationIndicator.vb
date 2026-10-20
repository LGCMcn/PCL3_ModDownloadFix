Imports System.Windows.Media.Animation
Imports System.Windows.Threading

'只在独立视觉树中绘制选中胶囊。按钮、焦点和页面仍由主界面线程管理。
Public Class MyNavigationIndicator
    Inherits FrameworkElement

    Private Host As HostVisual
    Private Session As IndicatorRenderer
    Private TargetX As Double, TargetWidth As Double
    Private Ink As DrawingGroup
    Private InkKey As String
    Private InkRetry As DispatcherOperation
    Private InkAttempts As Integer
    Public Shared ActiveRenderers As Integer

    Public Sub New()
        IsHitTestVisible = False
        SnapsToDevicePixels = False : UseLayoutRounding = False
    End Sub
    Protected Overrides ReadOnly Property VisualChildrenCount As Integer
        Get
            Return If(Host Is Nothing, 0, 1)
        End Get
    End Property
    Protected Overrides Function GetVisualChild(Index As Integer) As Visual
        If Index <> 0 OrElse Host Is Nothing Then Throw New ArgumentOutOfRangeException(NameOf(Index))
        Return Host
    End Function
    Private Sub StartRenderer() Handles Me.Loaded
        If Session IsNot Nothing Then Return
        Host = New HostVisual() : AddVisualChild(Host)
        Session = New IndicatorRenderer(Host, Sub() Dispatcher.BeginInvoke(New Action(AddressOf InvalidateVisual)))
        Session.SetInk(Ink)
        Session.Request(TargetX, TargetWidth, False)
        Session.Start()
        InvalidateVisual()
    End Sub
    Private Sub StopRenderer() Handles Me.Unloaded
        Dim Previous = Session : Session = Nothing
        Previous?.Shutdown()
        If InkRetry?.Status = DispatcherOperationStatus.Pending Then InkRetry.Abort()
        InkRetry = Nothing
        If Host IsNot Nothing Then RemoveVisualChild(Host) : Host = Nothing
    End Sub

    Public Sub SelectTarget(X As Double, Width As Double, Animate As Boolean)
        If Double.IsNaN(X) OrElse Double.IsInfinity(X) OrElse Width <= 0 OrElse Double.IsNaN(Width) OrElse Double.IsInfinity(Width) Then Return
        If Math.Abs(TargetX - X) < 0.001 AndAlso Math.Abs(TargetWidth - Width) < 0.001 AndAlso NavigationMotionEnabled() Then Return
        TargetX = X : TargetWidth = Width
        Session?.Request(X, Width, Animate AndAlso NavigationMotionEnabled())
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
        If Session Is Nothing OrElse Session.Failed Then InvalidateVisual()
    End Sub
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
        If Session Is Nothing Then Return Task.FromResult({TargetX, TargetWidth, 0.0})
        Return Session.ReadStateAsync()
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
            If Not Initialized OrElse Not Animate OrElse Math.Abs(PreviousX - X) < 0.01 Then Initialized = Width > 0 : Return
            Glide = New DoubleAnimation(PreviousX, X, TimeSpan.FromMilliseconds(210)) With {.EasingFunction = New SineEase With {.EasingMode = EasingMode.EaseInOut}, .FillBehavior = FillBehavior.Stop}
            Resize = New DoubleAnimation(PreviousWidth / 78, Width / 78, TimeSpan.FromMilliseconds(210)) With {.EasingFunction = New SineEase With {.EasingMode = EasingMode.EaseInOut}, .FillBehavior = FillBehavior.Stop}
            AddHandler Glide.Completed, AddressOf Completed
            Shift.BeginAnimation(TranslateTransform.XProperty, Glide, HandoffBehavior.SnapshotAndReplace)
            Stretch.BeginAnimation(ScaleTransform.ScaleXProperty, Resize, HandoffBehavior.SnapshotAndReplace)
        End Sub
        Private Sub ClearAnimation()
            If Glide IsNot Nothing Then RemoveHandler Glide.Completed, AddressOf Completed
            Shift?.BeginAnimation(TranslateTransform.XProperty, Nothing)
            Stretch?.BeginAnimation(ScaleTransform.ScaleXProperty, Nothing)
            Glide = Nothing : Resize = Nothing
        End Sub
        Private Sub Completed(Sender As Object, E As EventArgs)
            ClearAnimation()
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
