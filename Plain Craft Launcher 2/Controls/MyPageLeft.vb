Public Class MyPageLeft
    Inherits Grid
    Private Uuid As Integer = GetUuid()
    Private EntranceMotion As MyPageMotion
    Private NeedsControlReset As Boolean

    '执行逐个进入动画的控件
    Public Property AnimatedControl As String '需要在 Loaded 之后才能获取到控件，所以不能用 Binding 直接绑定（#6664）
        Get
            Return GetValue(AnimatedControlProperty)
        End Get
        Set(value As String)
            SetValue(AnimatedControlProperty, value)
        End Set
    End Property
    Public Shared ReadOnly AnimatedControlProperty As DependencyProperty = DependencyProperty.Register("AnimatedControl", GetType(String), GetType(MyPageLeft), New PropertyMetadata(Nothing))

    Public Sub CancelPageAnimation()
        EntranceMotion?.Dispose() : EntranceMotion = Nothing
        AniStop("PageLeft PageChange " & Uuid)
        If Not NeedsControlReset Then Return
        NeedsControlReset = False
        Opacity = 1 : RenderTransform = Nothing
        If AnimatedControl Is Nothing Then Return
        For Each Element In GetAllAnimControls(True)
            Element.Opacity = 1 : Element.RenderTransform = Nothing
            If TypeOf Element Is MyListItem Then CType(Element, MyListItem).IsMouseOverAnimationEnabled = True
        Next
    End Sub
    Public Sub TriggerShowAnimation()
        CancelPageAnimation()
        If AnimatedControl IsNot Nothing Then
            For Each Element In GetAllAnimControls(True)
                If TypeOf Element Is TextBlock Then Element.Opacity = 0.6
            Next
        End If
        If Not NavigationMotionEnabled() OrElse Not IsLoaded Then Return
        EntranceMotion = New MyPageMotion({CType(Me, FrameworkElement)}, Sub() EntranceMotion = Nothing, True)
    End Sub
    Public Sub TriggerHideAnimation()
        EntranceMotion?.Dispose() : EntranceMotion = Nothing
        NeedsControlReset = True
        If AnimatedControl Is Nothing Then
            '缩放动画
            If TypeOf RenderTransform IsNot ScaleTransform Then
                RenderTransform = New ScaleTransform(1, 1)
                RenderTransformOrigin = New Point(0.5, 0.5)
            End If
            AniStart({
                AaScaleTransform(Me, 0.95 - CType(RenderTransform, ScaleTransform).ScaleX, 110,, New AniEaseInFluent(AniEasePower.Weak)),
                AaOpacity(Me, -Opacity, 80, 30)
            }, "PageLeft PageChange " & Uuid)
        Else
            '逐个退出动画
            Dim AniList As New List(Of AniData)
            Dim Id As Integer = 0
            Dim Controls = GetAllAnimControls()
            For Each Element As FrameworkElement In Controls
                AniList.Add(AaOpacity(Element, -Element.Opacity, 50, 70 / Controls.Count * Id))
                AniList.Add(AaTranslateX(Element, -6, 50, 70 / Controls.Count * Id))
                Id += 1
            Next
            AniStart(AniList, "PageLeft PageChange " & Uuid)
        End If
    End Sub

    '遍历获取所有需要生成动画的控件
    Private Function GetAllAnimControls(Optional IgnoreInvisibility As Boolean = False) As List(Of FrameworkElement)
        Dim AllControls As New List(Of FrameworkElement)
        GetAllAnimControls(FindName(AnimatedControl), AllControls, IgnoreInvisibility)
        Return AllControls
    End Function
    Private Sub GetAllAnimControls(Element As FrameworkElement, ByRef AllControls As List(Of FrameworkElement), IgnoreInvisibility As Boolean)
        If Not IgnoreInvisibility AndAlso Element.Visibility = Visibility.Collapsed Then Return
        If TypeOf Element Is MyTextButton Then
            AllControls.Add(Element)
        ElseIf TypeOf Element Is MyListItem Then
            AllControls.Add(Element)
        ElseIf TypeOf Element Is ContentControl Then
            GetAllAnimControls(CType(Element, ContentControl).Content, AllControls, IgnoreInvisibility)
        ElseIf TypeOf Element Is Panel Then
            For Each Element2 As FrameworkElement In CType(Element, Panel).Children
                GetAllAnimControls(Element2, AllControls, IgnoreInvisibility)
            Next
        Else
            AllControls.Add(Element)
        End If
    End Sub

End Class
