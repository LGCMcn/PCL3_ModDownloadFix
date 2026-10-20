Imports System.Windows.Media.Animation
Imports System.Windows.Media.Effects
Imports System.Windows.Shapes

'只给背景与短时交互添加效果；不在文字上模糊，不创建常驻计时器或渲染事件。
Friend Module ModVisualExperience
    Public Sub ApplyQuietCards(Owner As DependencyObject)
        For Each Child In LogicalTreeHelper.GetChildren(Owner)
            Dim Visual = TryCast(Child, DependencyObject)
            If Visual Is Nothing Then Continue For
            Dim Card = TryCast(Visual, MyCard)
            If Card IsNot Nothing Then
                Card.MainTextBlock.FontSize = 16
                Card.MainTextBlock.FontWeight = FontWeights.SemiBold
                Card.MainTextBlock.Margin = New Thickness(24, 14, 0, 0)
                Card.MainChrome.Opacity = 0
                Card.HasMouseAnimation = False
                Card.CornerRadius = New CornerRadius(8)
            End If
            ApplyQuietCards(Visual)
        Next
    End Sub
    Public Function MotionAvailable() As Boolean
        Return SystemParameters.ClientAreaAnimation AndAlso Not SystemParameters.HighContrast
    End Function

    Public Sub ApplyVisualExperience()
        If FrmMain Is Nothing Then Return
        RunInUi(Sub() ConfigureGlass(FrmMain.GlassBackdrop, FrmMain.ImgBack, Settings.Get(Of Boolean)("UiFrostedGlass")))
    End Sub

    Public Sub ConfigureGlass(Layer As Border, Background As Visual, Enabled As Boolean)
        Layer.Child = Nothing
        Layer.Visibility = If(Enabled AndAlso Not SystemParameters.HighContrast, Visibility.Visible, Visibility.Collapsed)
        If Layer.Visibility <> Visibility.Visible Then Return
        Dim Surface As New Grid()
        If (RenderCapability.Tier >> 16) >= 2 Then
            Dim Frost As New Rectangle With {.Fill = New VisualBrush(Background) With {.Stretch = Stretch.Fill}, .IsHitTestVisible = False}
            Dim Blur As New BlurEffect With {.Radius = 12, .RenderingBias = RenderingBias.Performance}
            Blur.Freeze() : Frost.Effect = Blur
            Surface.Children.Add(Frost)
        End If
        Surface.Children.Add(New Border With {.Background = New SolidColorBrush(Color.FromArgb(194, 234, 245, 255)),
            .BorderBrush = New SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), .BorderThickness = New Thickness(0, 1, 0, 0)})
        Layer.Child = Surface
    End Sub

    Public Sub PlayErrorAccent(Target As FrameworkElement)
        If Not Settings.Get(Of Boolean)("UiErrorAnimation") OrElse Not MotionAvailable() Then Return
        BeginErrorAccent(Target)
    End Sub

    Public Sub BeginErrorAccent(Target As FrameworkElement)
        Dim Transform As New TranslateTransform()
        Target.RenderTransform = Transform
        Dim Motion As New DoubleAnimationUsingKeyFrames With {.Duration = TimeSpan.FromMilliseconds(340), .FillBehavior = FillBehavior.Stop}
        For Each Item In {Tuple.Create(0, 0.0), Tuple.Create(65, -4.0), Tuple.Create(135, 3.0), Tuple.Create(210, -1.5), Tuple.Create(340, 0.0)}
            Motion.KeyFrames.Add(New SplineDoubleKeyFrame(Item.Item2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(Item.Item1)), New KeySpline(0.2, 0.7, 0.3, 1)))
        Next
        Dim Finished As EventHandler = Nothing, Unloaded As RoutedEventHandler = Nothing
        Dim Cleanup As Action = Sub()
                                    Transform.BeginAnimation(TranslateTransform.XProperty, Nothing)
                                    If Object.ReferenceEquals(Target.RenderTransform, Transform) Then Target.RenderTransform = Nothing
                                    RemoveHandler Motion.Completed, Finished
                                    RemoveHandler Target.Unloaded, Unloaded
                                End Sub
        Finished = Sub() Cleanup()
        Unloaded = Sub() Cleanup()
        AddHandler Motion.Completed, Finished
        AddHandler Target.Unloaded, Unloaded
        Transform.BeginAnimation(TranslateTransform.XProperty, Motion, HandoffBehavior.SnapshotAndReplace)
    End Sub
End Module
