Imports System.Windows.Media.Animation

Public Class MyScrollViewer
    Inherits ScrollViewer
    Public Property DeltaMult As Double = 1
    Private TargetOffset As Double
    Private IsScrolling As Boolean
    Private HasPendingDirectOffset As Boolean
    Private SuppressOffset As Boolean
    Private ScrollGeneration As Integer
    Private Shared ReadOnly AnimatedOffsetProperty As DependencyProperty = DependencyProperty.Register(
        "AnimatedOffset", GetType(Double), GetType(MyScrollViewer), New PropertyMetadata(0.0,
        Sub(Sender, Args)
            Dim Viewer = CType(Sender, MyScrollViewer)
            If Not Viewer.SuppressOffset Then Viewer.ScrollToVerticalOffset(CDbl(Args.NewValue))
        End Sub))

    Private Sub PreviewWheel(sender As Object, e As MouseWheelEventArgs) Handles Me.PreviewMouseWheel
        If e.Delta = 0 OrElse ActualHeight = 0 OrElse ScrollableHeight <= 0 OrElse SystemParameters.WheelScrollLines = 0 Then Return
        '嵌套列表、文本框和弹出的下拉框先处理自己的滚动。
        Dim Node = TryCast(e.OriginalSource, DependencyObject)
        While Node IsNot Nothing AndAlso Not Object.ReferenceEquals(Node, Me)
            If TypeOf Node Is ComboBox AndAlso CType(Node, ComboBox).IsDropDownOpen Then Return
            Dim Inner = TryCast(Node, ScrollViewer)
            If Inner IsNot Nothing AndAlso Inner.ScrollableHeight > 0 AndAlso
                ((e.Delta > 0 AndAlso Inner.VerticalOffset > 0) OrElse (e.Delta < 0 AndAlso Inner.VerticalOffset < Inner.ScrollableHeight)) Then Return
            Node = If(TypeOf Node Is Visual OrElse TypeOf Node Is System.Windows.Media.Media3D.Visual3D, VisualTreeHelper.GetParent(Node), LogicalTreeHelper.GetParent(Node))
        End While
        Dim Distance = If(SystemParameters.WheelScrollLines < 0, ViewportHeight * 0.85, SystemParameters.WheelScrollLines * 20.0)
        Dim Delta = -e.Delta / 120.0 * Distance
        Dim Position = If(IsScrolling, TargetOffset, VerticalOffset)
        If (Delta < 0 AndAlso Position <= 0) OrElse (Delta > 0 AndAlso Position >= ScrollableHeight) Then
            e.Handled = IsScrolling : Return
        End If
        e.Handled = True
        PerformVerticalOffsetDelta(Delta)
    End Sub
    Public Sub PerformVerticalOffsetDelta(Delta As Double)
        If Double.IsNaN(Delta) OrElse Double.IsInfinity(Delta) Then Return
        Dim Current = VerticalOffset, Movement = Delta * DeltaMult
        Dim Origin = If(IsScrolling OrElse HasPendingDirectOffset, TargetOffset, Current)
        If IsScrolling AndAlso Math.Sign(Movement) <> Math.Sign(TargetOffset - Current) Then Origin = Current
        Dim Destination = Math.Max(0, Math.Min(ScrollableHeight, Origin + Movement))
        StopSmoothScroll() : TargetOffset = Destination
        If Not NavigationScrollEnabled() OrElse Math.Abs(Destination - Current) < 0.25 Then
            HasPendingDirectOffset = Math.Abs(Destination - Current) >= 0.25
            ScrollToVerticalOffset(Destination) : Return
        End If
        IsScrolling = True
        Dim Generation = ScrollGeneration
        SuppressOffset = True : SetValue(AnimatedOffsetProperty, Current) : SuppressOffset = False
        Dim Duration = TimeSpan.FromMilliseconds(Math.Min(210, 130 + Math.Abs(Destination - Current) * 0.18))
        Dim Motion As New DoubleAnimation(Current, Destination, Duration) With {.EasingFunction = New CubicEase With {.EasingMode = EasingMode.EaseOut}, .FillBehavior = FillBehavior.HoldEnd}
        Dim Finished As EventHandler = Nothing
        Finished = Sub()
                       RemoveHandler Motion.Completed, Finished
                       If Generation <> ScrollGeneration Then Return
                       StopSmoothScroll() : TargetOffset = Destination
                       ScrollToVerticalOffset(Destination)
                   End Sub
        AddHandler Motion.Completed, Finished
        BeginAnimation(AnimatedOffsetProperty, Motion, HandoffBehavior.SnapshotAndReplace)
    End Sub
    Public Sub StopSmoothScroll()
        ScrollGeneration += 1 : IsScrolling = False : HasPendingDirectOffset = False
        SuppressOffset = True
        BeginAnimation(AnimatedOffsetProperty, Nothing)
        SetValue(AnimatedOffsetProperty, VerticalOffset)
        SuppressOffset = False : TargetOffset = VerticalOffset
    End Sub
    Public ReadOnly Property HasSmoothScroll As Boolean
        Get
            Return IsScrolling
        End Get
    End Property
    Public Shadows Sub ScrollToHome()
        StopSmoothScroll() : MyBase.ScrollToHome()
    End Sub
    Private Sub InterruptInput() Handles Me.PreviewMouseDown, Me.PreviewKeyDown, Me.Unloaded
        StopSmoothScroll()
    End Sub
    Private Sub Changed(sender As Object, e As ScrollChangedEventArgs) Handles Me.ScrollChanged
        If e.ExtentHeightChange <> 0 OrElse e.ViewportHeightChange <> 0 Then StopSmoothScroll()
        If e.VerticalChange <> 0 Then HasPendingDirectOffset = False
        If Not IsScrolling AndAlso Not HasPendingDirectOffset Then TargetOffset = VerticalOffset
        If FrmMain IsNot Nothing AndAlso (e.VerticalChange <> 0 OrElse e.ViewportHeightChange <> 0) Then FrmMain.BtnExtraBack.ShowRefresh()
    End Sub
    Private Sub VisibilityChanged() Handles Me.IsVisibleChanged
        If Not IsVisible Then StopSmoothScroll()
        If FrmMain IsNot Nothing Then FrmMain.BtnExtraBack.ShowRefresh()
    End Sub
    Public ScrollBar As MyScrollBar
    Private Sub Load() Handles Me.Loaded
        ScrollBar = GetTemplateChild("PART_VerticalScrollBar")
    End Sub
    Private Sub PreviewFocus(sender As Object, e As KeyboardFocusChangedEventArgs) Handles Me.PreviewGotKeyboardFocus
        If TypeOf e.NewFocus Is MySlider Then e.Handled = True
    End Sub
End Class
