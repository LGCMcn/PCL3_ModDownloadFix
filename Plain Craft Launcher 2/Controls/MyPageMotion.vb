Imports System.Windows.Media.Animation
Imports System.Windows.Threading

'短时页面过渡：只移动内容容器，完成或离开页面时恢复原有变换。
Friend NotInheritable Class MyPageMotion
    Implements IDisposable
    Private Class Item
        Public Element As FrameworkElement
        Public OriginalTransform As Transform
        Public MotionTransform As TransformGroup
        Public Translation As TranslateTransform
        Public Axis As DependencyProperty
        Public Offset As Double
    End Class
    Private ReadOnly Items As New List(Of Item)
    Private Completion As Action
    Private Finished As EventHandler
    Private EndingMotion As DoubleAnimation
    Private Disposed As Boolean
    Private PendingStart As DispatcherOperation
    Private WaitingForFrame As Boolean
    Private Preparation As IDisposable
    Private NavigationLease As IDisposable

    Public Sub New(Elements As IEnumerable(Of FrameworkElement), OnFinished As Action, Optional Horizontal As Boolean = False)
        Completion = OnFinished
        For Each Element In Elements
            Dim State As New Item With {.Element = Element, .OriginalTransform = Element.RenderTransform,
                .Translation = New TranslateTransform(If(Horizontal, -10, 0), If(Horizontal, 0, 10)),
                .Axis = If(Horizontal, TranslateTransform.XProperty, TranslateTransform.YProperty), .Offset = If(Horizontal, -10, 10), .MotionTransform = New TransformGroup()}
            If State.OriginalTransform IsNot Nothing Then State.MotionTransform.Children.Add(State.OriginalTransform)
            State.MotionTransform.Children.Add(State.Translation)
            Element.RenderTransform = State.MotionTransform
            AddHandler Element.Unloaded, AddressOf Unloaded
            Items.Add(State)
        Next
        If Items.Count = 0 Then Finish() : Return
        '先提交初始姿态，再启动时钟。首次布局不能消耗掉整段动画的时间。
        WaitingForFrame = True
        Preparation = MeasureUiPhase("page.prepare-to-animation")
        AddHandler CompositionTarget.Rendering, AddressOf FirstFrame
    End Sub
    Private Sub FirstFrame(Sender As Object, Args As EventArgs)
        RemoveHandler CompositionTarget.Rendering, AddressOf FirstFrame
        WaitingForFrame = False
        If Disposed Then Return
        PendingStart = Items(0).Element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, New Action(AddressOf Start))
    End Sub
    Private Sub Start()
        PendingStart = Nothing
        If Disposed Then Return
        Preparation?.Dispose() : Preparation = Nothing
        Dim Owner = TryCast(Window.GetWindow(Items(0).Element), FormMain)
        NavigationLease = Owner?.TitleSelection?.BeginPageMotion()
        For Each State In Items
            Dim Element = State.Element
            Dim Slide As New DoubleAnimation(State.Offset, 0, TimeSpan.FromMilliseconds(180)) With {.EasingFunction = New QuadraticEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.Stop}
            If Object.ReferenceEquals(State, Items(Items.Count - 1)) Then
                EndingMotion = Slide : Finished = Sub() Finish()
                AddHandler Slide.Completed, Finished
            End If
            State.Translation.BeginAnimation(State.Axis, Slide, HandoffBehavior.SnapshotAndReplace)
        Next
    End Sub
    Private Sub Finish()
        If Disposed Then Return
        Dim Callback = Completion
        Dispose()
        Callback?.Invoke()
    End Sub
    Private Sub Unloaded(Sender As Object, Args As RoutedEventArgs)
        Dispose()
    End Sub
    Public Sub Dispose() Implements IDisposable.Dispose
        If Disposed Then Return
        Disposed = True : Completion = Nothing
        If WaitingForFrame Then RemoveHandler CompositionTarget.Rendering, AddressOf FirstFrame
        WaitingForFrame = False
        If PendingStart?.Status = DispatcherOperationStatus.Pending Then PendingStart.Abort()
        PendingStart = Nothing
        Preparation?.Dispose() : Preparation = Nothing
        NavigationLease?.Dispose() : NavigationLease = Nothing
        If EndingMotion IsNot Nothing Then RemoveHandler EndingMotion.Completed, Finished
        EndingMotion = Nothing : Finished = Nothing
        For Each State In Items
            RemoveHandler State.Element.Unloaded, AddressOf Unloaded
            State.Translation.BeginAnimation(State.Axis, Nothing)
            If Object.ReferenceEquals(State.Element.RenderTransform, State.MotionTransform) Then State.Element.RenderTransform = State.OriginalTransform
        Next
        Items.Clear()
    End Sub
End Class
