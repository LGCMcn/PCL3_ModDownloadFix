using System.ComponentModel;

namespace MeloongCore;

/// <summary>
/// 使用 Windows MsDelta API 生成、应用增量补丁文件。
/// </summary>
public static class MsDelta {

    /// <summary>
    /// 生成增量补丁文件：比较 <paramref name="oldFilePath"/> 和 <paramref name="newFilePath"/>，将补丁写入 <paramref name="deltaFilePath"/>。
    /// </summary>
    /// <exception cref="FileNotFoundException" />
    /// <exception cref="Win32Exception" />
    public static void Create(string oldFilePath, string newFilePath, string deltaFilePath) {
        if (!FileUtils.Exists(oldFilePath)) throw new FileNotFoundException($"旧文件不存在：{oldFilePath}", oldFilePath);
        if (!FileUtils.Exists(newFilePath)) throw new FileNotFoundException($"新文件不存在：{newFilePath}", newFilePath);
        DirectoryUtils.Create(PathUtils.RemoveLastPart(deltaFilePath));
        Logger.Info($"正在生成补丁：{oldFilePath} → {newFilePath}，补丁文件将输出到：{deltaFilePath}");
        // fileTypeSet：https://learn.microsoft.com/en-us/previous-versions/bb417345(v=msdn.10)?redirectedfrom=MSDN#file-type-sets (15L: DELTA_FILE_TYPE_SET_EXECUTABLES)
        // 131072L: IgnoreFileSizeLimit, 32u: Crc32
        if (!CreateDelta(15L, 131072L, 0L, 
            PathUtils.ForApi(oldFilePath), PathUtils.ForApi(newFilePath), null, null, new DeltaInput(), IntPtr.Zero, 32u, PathUtils.ForApi(deltaFilePath)))
            throw new Win32Exception();
    }

    /// <summary>
    /// 应用增量补丁文件：将 <paramref name="deltaFilePath"/> 补丁应用到 <paramref name="oldFilePath"/>，将结果写入 <paramref name="newFilePath"/>。
    /// </summary>
    /// <exception cref="FileNotFoundException" />
    /// <exception cref="Win32Exception" />
    public static void Apply(string oldFilePath, string deltaFilePath, string newFilePath) {
        if (!FileUtils.Exists(oldFilePath)) throw new FileNotFoundException($"旧文件不存在：{oldFilePath}", oldFilePath);
        if (!FileUtils.Exists(deltaFilePath)) throw new FileNotFoundException($"补丁文件不存在：{deltaFilePath}", deltaFilePath);
        DirectoryUtils.Create(PathUtils.RemoveLastPart(newFilePath));
        Logger.Info($"正在应用补丁：原始文件 {oldFilePath} + 补丁文件 {deltaFilePath} → 输出至 {newFilePath}");
        // 1L: AllowLegacy
        if (!ApplyDelta(1L, PathUtils.ForApi(oldFilePath), PathUtils.ForApi(deltaFilePath), PathUtils.ForApi(newFilePath)))
            throw new Win32Exception();
    }

    // API
    [StructLayout(LayoutKind.Sequential)]
    private struct DeltaInput {
        public IntPtr Start;
        public IntPtr Size;
        [MarshalAs(UnmanagedType.Bool)] public bool Editable;
    }
    [DllImport("msdelta.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDelta(
        long fileTypeSet, long setFlags, long resetFlags,
        string sourceName, string targetName, string? sourceOptionsName,
        string? targetOptionsName, DeltaInput globalOptions,
        IntPtr targetFileTime, uint hashAlgId, string deltaName);
    [DllImport("msdelta.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ApplyDelta(long applyFlags, string sourceName, string deltaName, string targetName);

}
