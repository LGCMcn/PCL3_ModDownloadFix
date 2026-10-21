Imports System.Windows.Media.Animation
Imports System.Windows.Threading

Friend Module ModNavigation
    '只保留少量常用页面；未激活页面不参与父容器的测量与排列。
    Private Class NavigationDeck
        Inherits Panel
        Private ReadOnly Retained As New List(Of MyPageRight)
        Public Property ActivePage As MyPageRight
        Public Sub RemovePage(Page As MyPageRight)
            Retained.Remove(Page) : Children.Remove(Page)
            If Object.ReferenceEquals(ActivePage, Page) Then ActivePage = Nothing
        End Sub
        Public Sub Preload(Page As MyPageRight, Available As Size)
            If Page Is Nothing OrElse Not Page.RetainNavigationLayout OrElse Children.Contains(Page) Then Return
            Page.Visibility = Visibility.Hidden
            Grid.SetIsSharedSizeScope(Page, True)
            Children.Add(Page) : Retained.Add(Page)
            Page.PrepareNavigationLayout()
            Page.Measure(Available) : Page.Arrange(New Rect(Available))
            Trim()
        End Sub
        Public Sub Activate(Page As MyPageRight)
            If ActivePage IsNot Nothing Then
                If ActivePage.RetainNavigationLayout Then
                    ActivePage.Visibility = Visibility.Hidden
                Else
                    Children.Remove(ActivePage)
                End If
            End If
            ActivePage = Page
            If Page IsNot Nothing Then
                If Not Children.Contains(Page) Then
                    Grid.SetIsSharedSizeScope(Page, True)
                    Children.Add(Page)
                End If
                Retained.Remove(Page)
                If Page.RetainNavigationLayout Then Retained.Add(Page)
                Page.Visibility = Visibility.Visible
            End If
            Trim() : InvalidateMeasure()
        End Sub
        Private Sub Trim()
            While Retained.Count > 4
                Dim Oldest = Retained.First(Function(Page) Not Object.ReferenceEquals(Page, ActivePage))
                RemovePage(Oldest)
            End While
        End Sub
        Protected Overrides Function MeasureOverride(Available As Size) As Size
            If ActivePage Is Nothing Then Return New Size()
            ActivePage.Measure(Available) : Return ActivePage.DesiredSize
        End Function
        Protected Overrides Function ArrangeOverride(Final As Size) As Size
            ActivePage?.Arrange(New Rect(Final)) : Return Final
        End Function
    End Class

    Private Function GetDeck(Host As Border) As NavigationDeck
        Dim Deck = TryCast(Host.Child, NavigationDeck)
        If Deck IsNot Nothing Then Return Deck
        Dim Previous = TryCast(Host.Child, MyPageRight)
        Host.Child = Nothing
        Deck = New NavigationDeck() : Host.Child = Deck
        If Previous IsNot Nothing Then Deck.Activate(Previous)
        Return Deck
    End Function
    Public Function GetCurrentRightPage(Host As Border) As MyPageRight
        Dim Deck = TryCast(Host.Child, NavigationDeck)
        Return If(Deck Is Nothing, TryCast(Host.Child, MyPageRight), Deck.ActivePage)
    End Function
    Public Sub PrepareNavigationHost(Host As Border)
        GetDeck(Host)
    End Sub
    Public Sub PreloadNavigationHost(Host As Border)
        Dim Deck = GetDeck(Host)
        Dim Available As New Size(Math.Max(320, Host.ActualWidth), Math.Max(240, Host.ActualHeight))
        Dim Factories As Func(Of MyPageRight)() = {
            Function() If(FrmDownloadLeft Is Nothing, Nothing, CType(FrmDownloadLeft.PageGet(), MyPageRight)),
            Function() If(FrmSetupLeft Is Nothing, Nothing, CType(FrmSetupLeft.PageGet(), MyPageRight)),
            Function() If(FrmOtherLeft Is Nothing, Nothing, CType(FrmOtherLeft.PageGet(), MyPageRight))}
        For Each Factory In Factories
            Try
                Dim Page = Factory()
                If Page Is Nothing OrElse Page.Parent IsNot Nothing Then Continue For
                Deck.Preload(Page, Available)
            Catch Ex As Exception
                Logger.Warn(Ex, "预加载导航页面失败，将在进入时重试")
            End Try
        Next
    End Sub
    Private ReadOnly PendingEnterProperty As DependencyProperty = DependencyProperty.RegisterAttached("PendingEnter", GetType(DispatcherOperation), GetType(MyPageRight))
    Private ReadOnly PendingPoseProperty As DependencyProperty = DependencyProperty.RegisterAttached("PendingPose", GetType(Transform), GetType(MyPageRight))

    Private Sub RestoreNavigationPose(Host As Border)
        If Host.ReadLocalValue(PendingPoseProperty) Is DependencyProperty.UnsetValue Then Return
        Host.RenderTransform = CType(Host.GetValue(PendingPoseProperty), Transform)
        Host.ClearValue(PendingPoseProperty)
    End Sub

    Public Sub PrepareNavigationPages()
        If FrmMain Is Nothing Then Return
        Dim Actions As New Queue(Of Action)({
            Sub()
                If FrmSetupLeft Is Nothing Then FrmSetupLeft = New PageSetupLeft()
                WarmPageLayout(CType(FrmSetupLeft.PageGet(), MyPageRight))
                If FrmSetupUI Is Nothing Then FrmSetupUI = New PageSetupUI()
                WarmPageLayout(FrmSetupUI)
                If FrmSetupSystem Is Nothing Then FrmSetupSystem = New PageSetupSystem()
                WarmPageLayout(FrmSetupSystem)
            End Sub,
            Sub()
                If FrmDownloadLeft Is Nothing Then FrmDownloadLeft = New PageDownloadLeft()
                WarmPageLayout(CType(FrmDownloadLeft.PageGet(), MyPageRight))
            End Sub,
            Sub()
                If FrmOtherLeft Is Nothing Then FrmOtherLeft = New PageOtherLeft()
                WarmPageLayout(CType(FrmOtherLeft.PageGet(), MyPageRight))
                If FrmOtherAbout Is Nothing Then FrmOtherAbout = New PageOtherAbout()
                WarmPageLayout(FrmOtherAbout)
                If FrmOtherTest Is Nothing Then FrmOtherTest = New PageOtherTest()
                WarmPageLayout(FrmOtherTest)
            End Sub})
        '在主窗口显示前准备常用页面，不在可交互阶段插入整页构造任务。
        While Actions.Count > 0 AndAlso Not IsProgramEnding
            Try
                Actions.Dequeue().Invoke()
            Catch Ex As Exception
                Logger.Warn(Ex, "提前准备导航页面失败，将在进入时重试")
            End Try
        End While
    End Sub

    Friend Sub WarmPageLayout(Page As MyPageRight)
        '构造完成后模板仍可能延迟到第一次绘制；提前完成常用页面的模板与布局。
        Dim Size As New Size(Math.Max(320, Settings.Get(Of Integer)("WindowWidth") - 176), Math.Max(240, Settings.Get(Of Integer)("WindowHeight") - 84))
        Page.PrepareNavigationLayout()
        Page.Measure(Size)
        Page.Arrange(New Rect(Size))
    End Sub

    Private Sub Detach(Target As FrameworkElement)
        Dim Parent = Target.Parent
        If Parent Is Nothing Then Return
        If TypeOf Parent Is NavigationDeck Then
            CType(Parent, NavigationDeck).RemovePage(CType(Target, MyPageRight))
        ElseIf TypeOf Parent Is Panel Then
            CType(Parent, Panel).Children.Remove(Target)
        ElseIf TypeOf Parent Is Decorator Then
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
        If Object.ReferenceEquals(GetCurrentRightPage(Host), Target) Then Return
        Dim Pending = TryCast(Host.GetValue(PendingEnterProperty), DispatcherOperation)
        If Pending IsNot Nothing AndAlso Pending.Status = DispatcherOperationStatus.Pending Then Pending.Abort()
        Host.ClearValue(PendingEnterProperty)
        RestoreNavigationPose(Host)
        Dim Deck = GetDeck(Host)
        Dim Previous = Deck.ActivePage
        Previous?.PageOnNavigationExit()
        If Target Is Nothing Then Deck.Activate(Nothing) : Host.Opacity = 1 : Return
        Dim Reused = Deck.Children.Contains(Target)
        If Not Reused Then Detach(Target)
        If Target.PageState <> MyPageRight.PageStates.Empty Then Target.PageOnForceExit()
        Target.Opacity = 1
        '第一帧就显示页面的起始姿态，不先绘制一个透明的整页。
        If NavigationMotionEnabled() Then
            Host.SetValue(PendingPoseProperty, Host.RenderTransform)
            Host.RenderTransform = New TranslateTransform(0, 10)
        End If
        Host.Opacity = 1
        Deck.Activate(Target) : Host.Background = Nothing
        '等待 WPF 的 Loaded 队列，不再等待退出动画或固定毫秒延迟。
        Dim Enter As New Action(
            Sub()
                If Not Object.ReferenceEquals(GetCurrentRightPage(Host), Target) Then Return
                Host.ClearValue(PendingEnterProperty)
                Try
                    If Reused Then Target.NotifyNavigationActivated()
                    If Target.PageState = MyPageRight.PageStates.Empty Then Target.PageOnEnter()
                Finally
                    If Object.ReferenceEquals(GetCurrentRightPage(Host), Target) Then RestoreNavigationPose(Host)
                End Try
            End Sub)
        If Reused AndAlso Target.IsLoaded Then
            Using MeasureUiPhase("navigation.reuse-entry")
                Enter()
            End Using
            Return
        End If
        Dim Operation = Host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, Enter)
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

    Public Sub MoveTitleSelection(Host As FrameworkElement, Selector As StackPanel, Indicator As MyNavigationIndicator, Animate As Boolean)
        Dim Selected = Selector.Children.OfType(Of MyRadioButton)().FirstOrDefault(Function(Item) Item.Checked AndAlso Item.Visibility = Visibility.Visible)
        If Selected Is Nothing Then Indicator.Visibility = Visibility.Collapsed : Return
        If Selected.ActualWidth <= 0 Then Return
        Dim Position = Selected.TranslatePoint(New Point(0, 0), Host)
        Indicator.Visibility = Visibility.Visible
        Indicator.UpdateInk(Host, Selector)
        Indicator.SelectTarget(Position.X, Selected.ActualWidth, Animate)
    End Sub
End Module
