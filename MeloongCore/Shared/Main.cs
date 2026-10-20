namespace MeloongCore;
public static class Main {

    public static string AppName = "Meloong";
    public static void Init(string appName) {
        AppName = appName;
        AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromSeconds(5));
    }

}
