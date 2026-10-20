Imports System.Windows.Media.Animation
Imports System.Windows.Threading

Friend Module ModNavigation
    Private ReadOnly PendingEnterProperty As DependencyProperty = DependencyProperty.RegisterAttached("PendingEnter", GetType(DispatcherOperation), GetType(MyPageRight))
    Private ReadOnly SelectionMotionProperty As DependencyProperty = DependencyProperty.RegisterAttached("SelectionMotion", GetType(TitleSelectionMotion), GetType(MyPageRight))

    Private Sub Detach(Target As FrameworkElement)
        Dim Parent = Target.Parent
        If Parent Is Nothing Then Return
        If TypeOf Parent Is Decorator Then
            CType(Parent, Decorator).Child = Nothing
        ElseIf TypeOf Parent Is ContentControl Then
            CType(Parent, ContentControl).Content = Nothing
        ElseIf TypeOf Parent Is ContentPresenter Then
            CType(Parent, ContentPresenter).Content = Nothing
        Else
            Throw New InvalidOperationException("页面仍属于其他容器。")
        End If
    End Sub

    Public Sub MountRightPage(Host As Border, Target As MyPageRight)
        If Object.ReferenceEquals(Host.Child, Target) Then Return
        Dim Pending = TryCast(Host.GetValue(PendingEnterProperty), DispatcherOperation)
        If Pending IsNot Nothing AndAlso Pending.Status = DispatcherOperationStatus.Pending Then Pending.Abort()
        Host.ClearValue(PendingEnterProperty)
        Dim Previous = TryCast(Host.Child, MyPageRight)
        Previous?.PageOnExit()
        Previous?.PageOnForceExit()
        If Target Is Nothing Then Host.Child = Nothing : Return
        Detach(Target)
        Target.PageOnForceExit()
        Target.Opacity = 1
        Host.Child = Target : Host.Background = Nothing
        '等待 WPF 的 Loaded 队列，不再等待退出动画或固定毫秒延迟。
        Dim Operation = Host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, New Action(
            Sub()
                If Not Object.ReferenceEquals(Host.Child, Target) Then Return
                Host.ClearValue(PendingEnterProperty)
                If Target.PageState = MyPageRight.PageStates.Empty Then Target.PageOnEnter()
            End Sub))
        Host.SetValue(PendingEnterProperty, Operation)
    End Sub

    Public Sub MountLeftPage(Host As Border, Target As MyPageLeft)
        If Object.ReferenceEquals(Host.Child, Target) Then Return
        Dim Pending = TryCast(Host.GetValue(PendingEnterProperty), DispatcherOperation)
        If Pending IsNot Nothing AndAlso Pending.Status = DispatcherOperationStatus.Pending Then Pending.Abort()
        Host.ClearValue(PendingEnterProperty)
        TryCast(Host.Child, MyPageLeft)?.CancelPageAnimation()
        Detach(Target) : Host.Child = Target : Host.Background = Nothing : Target.Opacity = 1
        Dim Operation = Host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, New Action(
            Sub()
                If Not Object.ReferenceEquals(Host.Child, Target) Then Return
                Host.ClearValue(PendingEnterProperty)
                Target.TriggerShowAnimation()
            End Sub))
        Host.SetValue(PendingEnterProperty, Operation)
    End Sub

    Public Sub MoveTitleSelection(Host As FrameworkElement, Selector As StackPanel, Indicator As Border, Animate As Boolean)
        Dim Selected = Selector.Children.OfType(Of MyRadioButton)().FirstOrDefault(Function(Item) Item.Checked AndAlso Item.Visibility = Visibility.Visible)
        If Selected Is Nothing Then Indicator.Visibility = Visibility.Collapsed : Return
        If Selected.ActualWidth <= 0 Then Return
        Dim Position = Selected.TranslatePoint(New Point(0, 0), Host)
        Dim Translation = TryCast(Indicator.RenderTransform, TranslateTransform)
        If Translation Is Nothing Then Translation = New TranslateTransform() : Indicator.RenderTransform = Translation
        Dim PreviousX = Translation.X, PreviousWidth = If(Double.IsNaN(Indicator.Width), 0, Indicator.Width)
        Dim Previous = TryCast(Host.GetValue(SelectionMotionProperty), TitleSelectionMotion)
        Previous?.Cancel() : Host.ClearValue(SelectionMotionProperty)
        Translation.X = Position.X : Indicator.Width = Selected.ActualWidth
        Indicator.Visibility = Visibility.Visible
        If Not Animate OrElse PreviousWidth <= 0 OrElse Not NavigationMotionEnabled() Then Return
        Dim Motion As New TitleSelectionMotion(Host, Indicator, Translation, PreviousX, Position.X, PreviousWidth, Selected.ActualWidth)
        Host.SetValue(SelectionMotionProperty, Motion)
        Motion.Begin()
    End Sub

    Private Class TitleSelectionMotion
        Private ReadOnly Host As FrameworkElement, Indicator As Border, Translation As TranslateTransform
        Private ReadOnly Glide As DoubleAnimation, Resize As DoubleAnimation
        Public Sub New(Owner As FrameworkElement, Capsule As Border, Shift As TranslateTransform, FromX As Double, ToX As Double, FromWidth As Double, ToWidth As Double)
            Host = Owner : Indicator = Capsule : Translation = Shift
            Glide = New DoubleAnimation(FromX, ToX, TimeSpan.FromMilliseconds(190)) With {.EasingFunction = New CubicEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.Stop}
            Resize = New DoubleAnimation(FromWidth, ToWidth, TimeSpan.FromMilliseconds(190)) With {.EasingFunction = New CubicEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.Stop}
        End Sub
        Public Sub Begin()
            AddHandler Glide.Completed, AddressOf Finished
            Translation.BeginAnimation(TranslateTransform.XProperty, Glide, HandoffBehavior.SnapshotAndReplace)
            Indicator.BeginAnimation(FrameworkElement.WidthProperty, Resize, HandoffBehavior.SnapshotAndReplace)
        End Sub
        Public Sub Cancel()
            RemoveHandler Glide.Completed, AddressOf Finished
            Translation.BeginAnimation(TranslateTransform.XProperty, Nothing)
            Indicator.BeginAnimation(FrameworkElement.WidthProperty, Nothing)
        End Sub
        Private Sub Finished(Sender As Object, E As EventArgs)
            Cancel()
            If Object.ReferenceEquals(Host.GetValue(SelectionMotionProperty), Me) Then Host.ClearValue(SelectionMotionProperty)
        End Sub
    End Class
End Module
