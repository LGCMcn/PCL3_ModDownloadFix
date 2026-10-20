Imports System.Windows.Media.Animation

'Spring integration adapted from morphicons src/core/spring.ts, MIT, Copyright (c) 2026 Guillermo.
'See ThirdParty/Morphicons/LICENSE. Fixed local stroke shapes use native WPF drawing only.
Public Class MyOfflineMorphIcon
    Inherits FrameworkElement
    Public Shared ReadOnly StateProperty As DependencyProperty = DependencyProperty.Register("State", GetType(String), GetType(MyOfflineMorphIcon), New PropertyMetadata("download", AddressOf StateChanged))
    Private Shared ReadOnly ProgressProperty As DependencyProperty = DependencyProperty.Register("Progress", GetType(Double), GetType(MyOfflineMorphIcon), New FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender))
    Private ReadOnly Ink As New Pen(New SolidColorBrush(Color.FromRgb(35, 101, 178)), 1.7) With {.StartLineCap = PenLineCap.Round, .EndLineCap = PenLineCap.Round}
    Private FromPoints As Double() = Shape("download")
    Private ToPoints As Double() = Shape("download")
    Public Property State As String
        Get
            Return CStr(GetValue(StateProperty))
        End Get
        Set(Value As String)
            SetValue(StateProperty, Value)
        End Set
    End Property
    Public Sub New()
        Width = 24 : Height = 24 : IsHitTestVisible = False
        Ink.Freeze()
        AddHandler Unloaded, AddressOf StopMotion
    End Sub
    Private Shared Sub StateChanged(Sender As DependencyObject, Args As DependencyPropertyChangedEventArgs)
        CType(Sender, MyOfflineMorphIcon).ChangeState(CStr(Args.NewValue))
    End Sub
    Private Sub ChangeState(Name As String)
        Dim Current = CDbl(GetValue(ProgressProperty))
        For I = 0 To FromPoints.Length - 1
            FromPoints(I) += (ToPoints(I) - FromPoints(I)) * Current
        Next
        ToPoints = Shape(Name)
        BeginAnimation(ProgressProperty, Nothing)
        If Not IsLoaded OrElse Not MotionAvailable() OrElse Not Settings.Get(Of Boolean)("UiIconAnimation") Then
            SetValue(ProgressProperty, 1.0) : Return
        End If
        SetValue(ProgressProperty, 0.0)
        Dim Values = SpringSamples()
        Dim Animation As New DoubleAnimationUsingKeyFrames With {.Duration = TimeSpan.FromSeconds(Values.Length / 60.0), .FillBehavior = FillBehavior.Stop}
        Animation.KeyFrames.Add(New DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)))
        For I = 0 To Values.Length - 1
            Animation.KeyFrames.Add(New LinearDoubleKeyFrame(Values(I), KeyTime.FromTimeSpan(TimeSpan.FromSeconds((I + 1) / 60.0))))
        Next
        'The final underlying value is already set. Stop releases the clock, without a render-loop subscription.
        SetValue(ProgressProperty, 1.0)
        Dim Finished As EventHandler = Nothing
        Finished = Sub()
                       BeginAnimation(ProgressProperty, Nothing)
                       RemoveHandler Animation.Completed, Finished
                   End Sub
        AddHandler Animation.Completed, Finished
        BeginAnimation(ProgressProperty, Animation, HandoffBehavior.SnapshotAndReplace)
    End Sub
    Private Sub StopMotion()
        BeginAnimation(ProgressProperty, Nothing)
        SetValue(ProgressProperty, 1.0)
    End Sub
    Public Shared Function SpringSamples() As Double()
        Dim X = 0.0, Velocity = 0.0, Samples As New List(Of Double)
        For Frame = 0 To 89
            'Semi-implicit Euler, 1/240 second substeps; upstream snappy preset k=420, c=30.
            For StepIndex = 0 To 3
                Velocity += (420 * (1 - X) - 30 * Velocity) / 240
                X += Velocity / 240
            Next
            Samples.Add(X)
            If Math.Abs(1 - X) < 0.001 AndAlso Math.Abs(Velocity) < 0.02 Then Exit For
        Next
        Samples(Samples.Count - 1) = 1
        Return Samples.ToArray()
    End Function
    Private Shared Function Shape(Name As String) As Double()
        Select Case Name
            Case "ready"
                Return {4.0, 12, 9, 17, 9, 17, 20, 6, 20, 6, 20, 6, 20, 6, 20, 6, 20, 6, 20, 6, 20, 6, 20, 6}
            Case "error"
                Return {6.0, 6, 18, 18, 6, 18, 18, 6, 18, 6, 18, 6, 18, 6, 18, 6, 18, 6, 18, 6, 18, 6, 18, 6}
            Case "device"
                Return {3.0, 4, 21, 4, 21, 4, 21, 16, 21, 16, 3, 16, 3, 16, 3, 4, 12, 16, 12, 21, 7, 21, 17, 21}
            Case "link"
                Return {14.0, 4, 20, 4, 20, 4, 20, 10, 20, 4, 11, 13, 5, 8, 5, 20, 5, 20, 17, 20, 17, 20, 17, 14}
            Case Else
                Return {12.0, 3, 12, 16, 7, 11, 12, 16, 12, 16, 17, 11, 4, 17, 4, 21, 4, 21, 20, 21, 20, 21, 20, 17}
        End Select
    End Function
    Protected Overrides Sub OnRender(Context As DrawingContext)
        MyBase.OnRender(Context)
        Dim Progress = CDbl(GetValue(ProgressProperty))
        For I = 0 To 5
            Dim Offset = I * 4
            Dim X1 = FromPoints(Offset) + (ToPoints(Offset) - FromPoints(Offset)) * Progress
            Dim Y1 = FromPoints(Offset + 1) + (ToPoints(Offset + 1) - FromPoints(Offset + 1)) * Progress
            Dim X2 = FromPoints(Offset + 2) + (ToPoints(Offset + 2) - FromPoints(Offset + 2)) * Progress
            Dim Y2 = FromPoints(Offset + 3) + (ToPoints(Offset + 3) - FromPoints(Offset + 3)) * Progress
            Context.DrawLine(Ink, New Point(X1 * ActualWidth / 24, Y1 * ActualHeight / 24), New Point(X2 * ActualWidth / 24, Y2 * ActualHeight / 24))
        Next
    End Sub
End Class
