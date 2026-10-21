Imports System.Collections.Concurrent
Imports System.Globalization
Imports System.Reflection
Imports System.Windows.Threading

'仅在显式设置 PCL_UI_PROFILE 时记录调度耗时，不记录按键内容、账号或网络凭据。
Friend Module ModUiDiagnostics
    Private ReadOnly Clock As Stopwatch = Stopwatch.StartNew()
    Private ReadOnly Records As New ConcurrentQueue(Of String)
    Private ReadOnly Started As New Dictionary(Of DispatcherOperation, Double)
    Private ReadOnly MethodField As FieldInfo = GetType(DispatcherOperation).GetField("_method", BindingFlags.Instance Or BindingFlags.NonPublic)
    Private Pulse As Threading.Timer
    Private ProbePending As Integer
    Private LastFrame As Double
    Private LastRenderTime As TimeSpan = TimeSpan.MinValue
    Private ReadOnly MotionSamples As New Dictionary(Of String, Tuple(Of Double, Double))
    Private Ui As Dispatcher
    Private Recording As Integer
    Public Function MeasureUiPhase(Name As String) As IDisposable
        If Threading.Volatile.Read(Recording) = 0 Then Return Nothing
        Return New PhaseMeasurement(Name)
    End Function
    Private NotInheritable Class PhaseMeasurement
        Implements IDisposable
        Private ReadOnly Name As String
        Private ReadOnly Begin As Double = Clock.Elapsed.TotalMilliseconds
        Public Sub New(Value As String)
            Name = Value
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            Write("phase", Clock.Elapsed.TotalMilliseconds - Begin, Name)
        End Sub
    End Class

    Public Sub StartUiDiagnostics()
        Dim Destination = Environment.GetEnvironmentVariable("PCL_UI_PROFILE")
        If String.IsNullOrWhiteSpace(Destination) OrElse Ui IsNot Nothing Then Return
        Threading.Interlocked.Exchange(Recording, 1)
        Ui = Application.Current.Dispatcher
        AddHandler Ui.Hooks.OperationStarted, AddressOf OperationStarted
        AddHandler Ui.Hooks.OperationCompleted, AddressOf OperationCompleted
        AddHandler CompositionTarget.Rendering, AddressOf Frame
        AddHandler InputManager.Current.PreProcessInput, AddressOf InputReceived
        Pulse = New Threading.Timer(
            Sub()
                If Ui.HasShutdownStarted OrElse Threading.Interlocked.CompareExchange(ProbePending, 1, 0) <> 0 Then Return
                Dim Posted = Clock.Elapsed.TotalMilliseconds
                Ui.BeginInvoke(DispatcherPriority.Input, New Action(
                    Sub()
                        Write("queue", Clock.Elapsed.TotalMilliseconds - Posted, "Input")
                        Threading.Interlocked.Exchange(ProbePending, 0)
                    End Sub))
            End Sub, Nothing, 0, 20)
        Dim Writer As New Thread(
            Sub()
                Try
                    Using Output As New IO.StreamWriter(Destination, False, New Text.UTF8Encoding(False))
                        Output.WriteLine("elapsed_ms,kind,duration_ms,detail")
                        Do While Threading.Volatile.Read(Recording) <> 0 AndAlso Not Ui.HasShutdownStarted AndAlso Clock.Elapsed.TotalMinutes < 5
                            Flush(Output)
                            Thread.Sleep(100)
                        Loop
                        Pulse.Dispose()
                        Flush(Output)
                    End Using
                Catch
                    Pulse.Dispose()
                Finally
                    Threading.Interlocked.Exchange(Recording, 0)
                    If Not Ui.HasShutdownStarted Then Ui.BeginInvoke(New Action(AddressOf Detach))
                End Try
            End Sub) With {.IsBackground = True, .Name = "UI timing recorder"}
        Writer.Start()
    End Sub

    Private Sub Detach()
        Threading.Interlocked.Exchange(Recording, 0)
        Pulse?.Dispose()
        RemoveHandler Ui.Hooks.OperationStarted, AddressOf OperationStarted
        RemoveHandler Ui.Hooks.OperationCompleted, AddressOf OperationCompleted
        RemoveHandler CompositionTarget.Rendering, AddressOf Frame
        RemoveHandler InputManager.Current.PreProcessInput, AddressOf InputReceived
        Started.Clear()
        MotionSamples.Clear()
    End Sub
    Private Sub Flush(Output As IO.StreamWriter)
        Dim Record As String = Nothing
        While Records.TryDequeue(Record)
            Output.WriteLine(Record)
        End While
        Output.Flush()
    End Sub
    Private Sub Write(Kind As String, Duration As Double, Detail As String)
        If Threading.Volatile.Read(Recording) = 0 Then Return
        Records.Enqueue(Clock.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) & "," & Kind & "," &
                        Duration.ToString("F3", CultureInfo.InvariantCulture) & "," & Detail.Replace(",", ";"))
    End Sub
    Private Sub OperationStarted(Sender As Object, Args As DispatcherHookEventArgs)
        Started(Args.Operation) = Clock.Elapsed.TotalMilliseconds
    End Sub
    Private Sub OperationCompleted(Sender As Object, Args As DispatcherHookEventArgs)
        Dim Begin As Double
        If Not Started.TryGetValue(Args.Operation, Begin) Then Return
        Started.Remove(Args.Operation)
        Dim Duration = Clock.Elapsed.TotalMilliseconds - Begin
        If Duration < 12 Then Return
        Dim Callback = TryCast(MethodField?.GetValue(Args.Operation), [Delegate])
        Write("operation", Duration, Args.Operation.Priority.ToString() & ":" & If(Callback Is Nothing, "unknown", Callback.Method.DeclaringType.FullName & "." & Callback.Method.Name))
    End Sub
    Private Sub Frame(Sender As Object, Args As EventArgs)
        Dim RenderTime = CType(Args, RenderingEventArgs).RenderingTime
        If RenderTime = LastRenderTime Then Return
        LastRenderTime = RenderTime
        Dim Now = Clock.Elapsed.TotalMilliseconds
        If LastFrame > 0 Then Write("frame", Now - LastFrame, "render")
        LastFrame = Now
        For Each Pair In AniGroups
            If Not Pair.Value.Data.Any(Function(Item) Item.TimeFinished > 0 AndAlso Item.TypeMain <> AniType.Code) Then Continue For
            Dim Progress = Pair.Value.Data.Sum(Function(Item) Item.TimeFinished)
            Dim Previous As Tuple(Of Double, Double) = Nothing
            If MotionSamples.TryGetValue(Pair.Key, Previous) AndAlso Progress <> Previous.Item1 Then Write("motion", Now - Previous.Item2, "group-" & Pair.Value.Uuid)
            If Previous Is Nothing OrElse Progress <> Previous.Item1 Then MotionSamples(Pair.Key) = Tuple.Create(Progress, Now)
        Next
        For Each Name In MotionSamples.Keys.Where(Function(Key) Not AniGroups.ContainsKey(Key)).ToArray()
            MotionSamples.Remove(Name)
        Next
    End Sub
    Private Sub InputReceived(Sender As Object, Args As PreProcessInputEventArgs)
        Dim Input = Args.StagingItem.Input
        If TypeOf Input Is MouseButtonEventArgs OrElse TypeOf Input Is MouseWheelEventArgs OrElse TypeOf Input Is KeyEventArgs Then
            Dim Delay = Math.Max(0, CInt(Environment.TickCount - Input.Timestamp))
            Write("input", Delay, Input.RoutedEvent.Name)
        End If
    End Sub
End Module
