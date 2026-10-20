Imports System.Management
Imports System.Runtime.InteropServices
Imports Microsoft.Win32

Friend Module ModDeviceInfo
    <StructLayout(LayoutKind.Sequential)>
    Private Class MemoryStatus
        Public Length As UInteger = CUInt(Marshal.SizeOf(GetType(MemoryStatus)))
        Public Load As UInteger
        Public TotalPhysical As ULong
        Public AvailablePhysical As ULong
        Public TotalPage As ULong
        Public AvailablePage As ULong
        Public TotalVirtual As ULong
        Public AvailableVirtual As ULong
        Public AvailableExtended As ULong
    End Class
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function GlobalMemoryStatusEx(Status As MemoryStatus) As Boolean
    End Function
    Private ReadOnly GpuGate As New Object()
    Private GpuQuery As Task(Of String)

    Public Function ReadBasicDeviceInfo() As String
        Dim WindowsName = "Windows（版本读取受限）"
        Try
            Using Key = Registry.LocalMachine.OpenSubKey("SOFTWARE\Microsoft\Windows NT\CurrentVersion")
                Dim Build = CStr(Key?.GetValue("CurrentBuildNumber", "")), Product = CStr(Key?.GetValue("ProductName", "Windows"))
                Dim Number As Integer
                If Integer.TryParse(Build, Number) AndAlso Number >= 22000 Then Product = Product.Replace("Windows 10", "Windows 11")
                WindowsName = Product & " " & CStr(Key?.GetValue("DisplayVersion", "")) & "（Build " & Build & "）"
            End Using
        Catch
        End Try
        Dim Result As New List(Of String) From {"系统：" & WindowsName,
            "架构：" & If(Environment.Is64BitOperatingSystem, "64 位 Windows", "32 位 Windows") & " / " & If(Environment.Is64BitProcess, "64 位进程", "32 位进程")}
        Try
            Using Key = Registry.LocalMachine.OpenSubKey("HARDWARE\DESCRIPTION\System\CentralProcessor\0")
                Result.Add("处理器：" & CStr(Key?.GetValue("ProcessorNameString", "未知")).Trim() & " / " & Environment.ProcessorCount & " 逻辑处理器")
            End Using
        Catch
            Result.Add("处理器：" & Environment.ProcessorCount & " 逻辑处理器（型号读取受限）")
        End Try
        Dim Memory As New MemoryStatus()
        If GlobalMemoryStatusEx(Memory) Then Result.Add("内存：" & (Memory.TotalPhysical / 1073741824).ToString("F1") & " GiB / 当前可用 " & (Memory.AvailablePhysical / 1073741824).ToString("F1") & " GiB")
        Try
            Dim Disk As New DriveInfo(Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory))
            Result.Add("程序所在磁盘：" & Disk.Name & " 可用 " & (Disk.AvailableFreeSpace / 1073741824).ToString("F1") & " GiB / 总计 " & (Disk.TotalSize / 1073741824).ToString("F1") & " GiB")
        Catch
            Result.Add("程序所在磁盘：暂时无法读取容量")
        End Try
        Result.Add("运行环境：.NET Framework 4.8 / CLR " & Environment.Version.ToString() & " / 第三方版本 v" & DownloadFixVersion)
        Return String.Join(vbCrLf & vbCrLf, Result)
    End Function

    Private Function ReadGraphicsInfo() As String
        Try
            Dim Options As New EnumerationOptions With {.Timeout = TimeSpan.FromSeconds(3), .ReturnImmediately = False, .Rewindable = False}
            Using Search As New ManagementObjectSearcher(New ManagementScope("\\.\root\cimv2"), New ObjectQuery("SELECT Name, DriverVersion FROM Win32_VideoController"), Options), Results = Search.Get()
                Dim Names As New List(Of String)
                For Each Item As ManagementObject In Results
                    Using Item
                        Names.Add(CStr(Item("Name")) & "（驱动 " & CStr(Item("DriverVersion")) & "）")
                    End Using
                    If Names.Count = 8 Then Exit For
                Next
                Return "显卡：" & If(Names.Count = 0, "未检测到可用信息", String.Join(vbCrLf & "      ", Names))
            End Using
        Catch
            Return "显卡：读取受限或 WMI 不可用，不影响启动器使用"
        End Try
    End Function

    Public Async Function ReadDeviceInfo(Optional Refresh As Boolean = False) As Task(Of String)
        'WMI 最多存在一个查询；超时后不反复启动后台线程。没有定时采样或设备标识采集。
        Dim Query As Task(Of String)
        SyncLock GpuGate
            If GpuQuery Is Nothing OrElse (Refresh AndAlso GpuQuery.IsCompleted) Then GpuQuery = Task.Run(Function() ReadGraphicsInfo())
            Query = GpuQuery
        End SyncLock
        Dim Basic = Await Task.Run(Function() ReadBasicDeviceInfo()).ConfigureAwait(False)
        If Await Task.WhenAny(Query, Task.Delay(3500)).ConfigureAwait(False) Is Query Then
            Return Basic & vbCrLf & vbCrLf & Await Query.ConfigureAwait(False)
        End If
        Return Basic & vbCrLf & vbCrLf & "显卡：查询较慢，已停止等待；稍后点击刷新重试"
    End Function
End Module
