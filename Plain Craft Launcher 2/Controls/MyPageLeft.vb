Public Class MyPageLeft
    Inherits Grid
    Private Uuid As Integer = GetUuid()

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
        AniStop("PageLeft PageChange " & Uuid)
        Opacity = 1 : RenderTransform = Nothing
        If AnimatedControl Is Nothing Then Return
        For Each Element In GetAllAnimControls(True)
            Element.Opacity = 1 : Element.RenderTransform = Nothing
            If TypeOf Element Is MyListItem Then CType(Element, MyListItem).IsMouseOverAnimationEnabled = True
        Next
    End Sub
    Public Sub TriggerShowAnimation()
        CancelPageAnimation()
        If Not NavigationMotionEnabled() Then Return
        If AnimatedControl Is Nothing Then
            '缩放动画
            If TypeOf RenderTransform IsNot ScaleTransform Then
                RenderTransform = New ScaleTransform(0.99, 0.99)
                RenderTransformOrigin = New Point(0.5, 0.5)
            End If
            Opacity = 0.94
            AniStart({
                AaScaleTransform(Me, 1 - CType(RenderTransform, ScaleTransform).ScaleX, 170,, New AniEaseOutFluent),
                AaOpacity(Me, 0.06, 90)
            }, "PageLeft PageChange " & Uuid)
        Else
            '逐个进入动画
            Dim AniList As New List(Of AniData)
            Dim Id As Integer = 0, Delay As Integer = 0
            For Each Element As FrameworkElement In GetAllAnimControls(True)
                Element = MyVirtualizingElement.TryInit(Element)
                If Element.Visibility = Visibility.Collapsed Then
                    '还原之前的隐藏动画可能导致的改变（#2436）
                    Element.Opacity = 1
                    Element.RenderTransform = New TranslateTransform(0, 0)
                    If TypeOf Element Is MyListItem Then CType(Element, MyListItem).IsMouseOverAnimationEnabled = True
                Else
                    Element.Opacity = 0.94
                    Element.RenderTransform = New TranslateTransform(-8, 0)
                    If TypeOf Element Is MyListItem Then CType(Element, MyListItem).IsMouseOverAnimationEnabled = True
                    AniList.Add(AaOpacity(Element, If(TypeOf Element Is TextBlock, -0.34, 0.06), 90, Delay, New AniEaseOutFluent(AniEasePower.Weak)))
                    AniList.Add(AaTranslateX(Element, 8, 160, Delay, New AniEaseOutFluent))
                    If TypeOf Element Is MyListItem Then
                        AniList.Add(AaCode(
                        Sub()
                            CType(Element, MyListItem).IsMouseOverAnimationEnabled = True
                            CType(Element, MyListItem).RefreshColor(Me, New EventArgs)
                        End Sub, Delay + 160))
                    End If
                    Delay = Math.Min(18, Delay + 6)
                    Id += 1
                End If
            Next
            AniStart(AniList, "PageLeft PageChange " & Uuid)
        End If
    End Sub
    Public Sub TriggerHideAnimation()
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
