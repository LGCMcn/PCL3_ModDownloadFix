namespace MeloongCore.Tests;

/// <summary>
/// 所有测试的基类。
/// 会初始化 MeloongCore 并重定向日志输出。
/// </summary>
public abstract class TestBase {

    public TestBase() {
        MeloongCore.Main.Init("MeloongCoreTest");
        Logger.Instance = new TestLogger();
    }
    private class TestLogger : BaseLogger {
        public override void HandleBehavior(string? rawMessage, string formattedMessage, LogBehavior behavior, Exception? ex)
            => TestContext.Current!.OutputWriter.WriteLine(formattedMessage);
    }

}

/// <summary>
/// 用于带文件测试的基类。
/// 会在极端路径下创建测试用的临时文件夹。
/// </summary>
public abstract class TestWithFolder : TestBase {

    /// <summary>
    /// 测试用的临时文件夹路径。
    /// 这是一个包含特殊字符的长路径，不以 \\?\ 开头，以 \ 结尾。
    /// </summary>
    public readonly string tempFolder;
    public TestWithFolder() : base() {
        tempFolder = Path.Combine(
            Path.GetTempPath(), "PCL", "Tests",
            $"{GetType().Name}-{Guid.NewGuid()}",
            "文件夹 Dir_!@#$%^&()_+={}[];',_",
            new string('X', 200), new string('X', 200)) + @"\";
        DirectoryUtils.Create(tempFolder);
    }

    /// <summary>
    /// 输出指定的测试用文件，返回文件路径。
    /// </summary>
    public string GetTestFile(string fileName) {
        var sourceFilePath = Path.Combine(Paths.Base, "TestFiles", GetType().Name.Replace("Test", ""), fileName);
        var distPath = Path.Combine(tempFolder, PathUtils.GetLastPart(sourceFilePath));
        FileUtils.Copy(sourceFilePath, distPath);
        return distPath;
    }

}
