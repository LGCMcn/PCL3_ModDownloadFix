Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Windows.Media.Imaging

'仅捕获无遮挡的本程序窗口，位图只供一次最小化使用，不写入磁盘。
Friend Module ModWindowSnapshot
    Public Function CaptureVisibleWindow(Handle As IntPtr, Bounds As Rect, Dpi As Double) As BitmapSource
        If Handle = IntPtr.Zero OrElse Not IsWindowVisible(Handle) OrElse IsIconic(Handle) Then Return Nothing
        Dim Width = CInt(Bounds.Width), Height = CInt(Bounds.Height)
        If Width <= 0 OrElse Height <= 0 OrElse CLng(Width) * Height > 8000000 Then Return Nothing
        Dim Desktop As New Rect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79))
        If Not Desktop.Contains(Bounds) Then Return Nothing
        Dim Above = GetWindow(Handle, 3), Count As Integer
        While Above <> IntPtr.Zero
            Count += 1
            If Count > 512 Then Return Nothing
            Dim Frame As CaptureRect
            If IsWindowVisible(Above) AndAlso GetWindowRect(Above, Frame) Then
                Dim Area As New Rect(Frame.Left, Frame.Top, Math.Max(0, Frame.Right - Frame.Left), Math.Max(0, Frame.Bottom - Frame.Top))
                If Area.Width > 0 AndAlso Area.Height > 0 AndAlso Bounds.IntersectsWith(Area) Then Return Nothing
            End If
            Above = GetWindow(Above, 3)
        End While
        Dim Screen = GetDC(IntPtr.Zero), Memory As IntPtr = IntPtr.Zero, Bitmap As IntPtr = IntPtr.Zero, Previous As IntPtr = IntPtr.Zero
        Try
            If Screen = IntPtr.Zero Then Return Nothing
            Memory = CreateCompatibleDC(Screen)
            If Memory = IntPtr.Zero Then Return Nothing
            Dim Info As New CaptureBitmapInfo With {.Size = 40, .Width = Width, .Height = -Height, .Planes = 1, .BitCount = 32}
            Dim Bits As IntPtr
            Bitmap = CreateDIBSection(Screen, Info, 0, Bits, IntPtr.Zero, 0)
            If Bitmap = IntPtr.Zero OrElse Bits = IntPtr.Zero Then Return Nothing
            Previous = SelectObject(Memory, Bitmap)
            If Not BitBlt(Memory, 0, 0, Width, Height, Screen, CInt(Bounds.Left), CInt(Bounds.Top), &H40CC0020) Then Return Nothing
            Dim Stride = Width * 4, Pixels(Stride * Height - 1) As Byte
            Marshal.Copy(Bits, Pixels, 0, Pixels.Length)
            Dim Result = BitmapSource.Create(Width, Height, 96 * Dpi, 96 * Dpi, PixelFormats.Bgr32, Nothing, Pixels, Stride)
            Result.Freeze() : Return Result
        Finally
            If Previous <> IntPtr.Zero AndAlso Memory <> IntPtr.Zero Then SelectObject(Memory, Previous)
            If Bitmap <> IntPtr.Zero Then DeleteObject(Bitmap)
            If Memory <> IntPtr.Zero Then DeleteDC(Memory)
            If Screen <> IntPtr.Zero Then ReleaseDC(IntPtr.Zero, Screen)
        End Try
    End Function
    <StructLayout(LayoutKind.Sequential)> Private Structure CaptureRect
        Public Left, Top, Right, Bottom As Integer
    End Structure
    <StructLayout(LayoutKind.Sequential)> Private Structure CaptureBitmapInfo
        Public Size As Integer
        Public Width, Height As Integer
        Public Planes, BitCount As UShort
        Public Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ColorsUsed, ColorsImportant As Integer
    End Structure
    <DllImport("user32.dll")> Private Function GetWindow(Handle As IntPtr, Command As Integer) As IntPtr
    End Function
    <DllImport("user32.dll")> Private Function IsWindowVisible(Handle As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll")> Private Function IsIconic(Handle As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll")> Private Function GetWindowRect(Handle As IntPtr, ByRef Bounds As CaptureRect) As Boolean
    End Function
    <DllImport("user32.dll")> Private Function GetSystemMetrics(Index As Integer) As Integer
    End Function
    <DllImport("user32.dll")> Private Function GetDC(Handle As IntPtr) As IntPtr
    End Function
    <DllImport("user32.dll")> Private Function ReleaseDC(Handle As IntPtr, Dc As IntPtr) As Integer
    End Function
    <DllImport("gdi32.dll")> Private Function CreateCompatibleDC(Dc As IntPtr) As IntPtr
    End Function
    <DllImport("gdi32.dll")> Private Function CreateDIBSection(Dc As IntPtr, ByRef Info As CaptureBitmapInfo, Usage As Integer, ByRef Bits As IntPtr, Section As IntPtr, Offset As Integer) As IntPtr
    End Function
    <DllImport("gdi32.dll")> Private Function SelectObject(Dc As IntPtr, Item As IntPtr) As IntPtr
    End Function
    <DllImport("gdi32.dll")> Private Function BitBlt(Destination As IntPtr, X As Integer, Y As Integer, Width As Integer, Height As Integer, Source As IntPtr, SourceX As Integer, SourceY As Integer, Operation As Integer) As Boolean
    End Function
    <DllImport("gdi32.dll")> Private Function DeleteObject(Item As IntPtr) As Boolean
    End Function
    <DllImport("gdi32.dll")> Private Function DeleteDC(Dc As IntPtr) As Boolean
    End Function
End Module
