using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Resources;
using Newtonsoft.Json.Linq;
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

class SmokeTests {
    static Assembly app;
    static int passed;
    static object Call(string type, string method, params object[] supplied) {
        var target = app.GetType("PCL." + type, true).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .First(m => m.Name == method && !m.ContainsGenericParameters && m.GetParameters().Length >= supplied.Length &&
                m.GetParameters().Skip(supplied.Length).All(p => p.IsOptional));
        var args = target.GetParameters().Select((p,i) => i < supplied.Length ? supplied[i] : Type.Missing).ToArray();
        try { return target.Invoke(null,args); }
        catch (TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    static void Check(bool ok,string name) {
        if (!ok) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name); passed++;
    }
    static bool HasKey(string url) {
        using(var req=new HttpRequestMessage(HttpMethod.Get,url)) {
            Call("ModSecret","SecretHeadersSign",url,req,false);
            return req.Headers.Contains("x-api-key");
        }
    }
    [STAThread] static int Main(string[] args) {
        try {
            app=Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Plain Craft Launcher 2.exe"));
            var application=(System.Windows.Application)Activator.CreateInstance(app.GetType("PCL.Application"),true);
            // A dispatcher frame can raise WPF Startup even without Application.Run.
            // Detach the production startup before any test initializes WPF or pumps messages.
            var startup=app.GetType("PCL.Application").GetMethod("Application_Startup",BindingFlags.Instance|BindingFlags.NonPublic);
            application.Startup-=(System.Windows.StartupEventHandler)Delegate.CreateDelegate(typeof(System.Windows.StartupEventHandler),application,startup);

            var resource=app.GetManifestResourceNames().Single(n=>n.EndsWith("Resources.resources"));
            var manager=new ResourceManager(resource.Substring(0,resource.Length-10),app);
            AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
                string name=new AssemblyName(e.Name).Name;
                if(!new[]{"CacheCow.Client","CacheCow.Client.FileStore","CacheCow.Common","ThrottleDebounce","Newtonsoft.Json","Ookii.Dialogs.Wpf","NAudio","Imazen.WebP","MeloongCore","MeloongCore.Wpf","Microsoft.Win32.Registry","PCLCS","System.Net.Http.Formatting"}.Contains(name)) return null;
                try { var bytes=manager.GetObject(name) as byte[]; return bytes==null?null:Assembly.Load(bytes); }
                catch { return null; }
            };
            // 和应用启动相同的核心初始化，但不启动 UI 或登录。
            var core=Assembly.Load("MeloongCore");
            core.GetType("MeloongCore.Main").GetMethod("Init").Invoke(null,new object[]{"PCLDownloadFixTest"});
            var wpf=Assembly.Load("MeloongCore.Wpf");
            wpf.GetType("MeloongCore.Wpf.Main").GetMethod("Init").Invoke(null,null);
            if(args.Contains("--personalization")) {
                app.GetType("PCL.Application").GetMethod("InitializeComponent").Invoke(application,null);
                Personalization();
                Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0);
            }
            if(args.Contains("--v5")) {
                app.GetType("PCL.Application").GetMethod("InitializeComponent").Invoke(application,null);
                V5Checks(); Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0);
            }
            if(args.Contains("--extra-network")) {
                Call("ModBase","RunInUi"); // Initialize UI thread identity before creating the request worker.
                var thread = new System.Threading.Thread(() => {
                    try {
                        Check(((System.Collections.IList)Call("ModExtraSources","FetchExtraProjects","GitHub","CaffeineMC/sodium")).Count==1,"real GitHub API project lookup");
                        Check(((System.Collections.IList)Call("ModExtraSources","FetchExtraProjects","FTB","stoneblock")).Count>0,"real FTB API search and details");
                        Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0);
                    } catch(Exception e) {Console.WriteLine(e); Environment.Exit(1);}
                }); thread.Start(); thread.Join();
            }
            if(args.Contains("--custom-config")) { CustomConfig(); Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0); }
            if(args.Contains("--invalid-config")) {
                try {Call("ModServices","ServiceValue","MicrosoftClientId","",null); throw new Exception("invalid config accepted");}
                catch(Exception e) {Check(e.Message.Contains("JSON"),"invalid JSON gives actionable error");}
                Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0);
            }
            Console.WriteLine("INIT: initialize launcher base");
            Check((long)Call("ModBase","GetTimeMs")>0,"launcher base initializes and returns monotonic time");
            Check(!(bool)app.GetType("PCL.ModSecret").GetField("RestrictMcDownload").GetRawConstantValue(),"game downloads enabled");
            Check(!(bool)Call("Settings","Get","UiHiddenPageLink"),"multiplayer page is no longer forcibly hidden");
            Check((string)Call("ModServices","ReplaceServiceBase","https://api.modrinth.com/v2/project/x?test=1","https://api.modrinth.com","https://mirror.test/modrinth")=="https://mirror.test/modrinth/v2/project/x?test=1","rewrite keeps path and query");
            Check((string)Call("ModServices","ReplaceServiceBase","https://evil.test/?u=https://api.modrinth.com/x","https://api.modrinth.com","https://mirror.test")=="https://evil.test/?u=https://api.modrinth.com/x","query domain is not rewritten");
            Check((string)Call("ModServices","ReplaceServiceBase","https://api.modrinth.com.evil.test/v2/x","https://api.modrinth.com","https://mirror.test")=="https://api.modrinth.com.evil.test/v2/x","domain suffix is not rewritten");
            Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY","local-test-value-not-a-real-key");
            Check(HasKey("https://api.curseforge.com/v1/mods/238222"),"CurseForge API receives configured key");
            Check(HasKey("https://edge.forgecdn.net/files/1234/1/test.jar"),"CurseForge CDN supports 2026 authentication");
            Check(!HasKey("https://api.modrinth.com/v2/search"),"Modrinth does not receive CurseForge key");
            Check(!HasKey("https://mod.mcimirror.top/curseforge/v1/mods/238222"),"third-party mirror does not receive key");
            Check(!HasKey("https://evil.test/?api.curseforge.com"),"query cannot receive key");
            Check(!HasKey("https://edge.forgecdn.net.evil.test/files/a.jar"),"suffix host cannot receive key");
            Environment.SetEnvironmentVariable("PCL_CURSEFORGE_API_KEY",null);
            Check(!HasKey("https://edge.forgecdn.net/files/a.jar"),"missing key is not sent as empty header");
            Check((string)Call("ModDownload","DlSourceModGet","https://api.modrinth.com/v2/search")=="https://api.modrinth.com/v2/search","disabled mirror preserves direct API");
            Check((string)Call("ModSecret","SecretCdnSign","https://example.test/a b{CDN}")=="https://example.test/a%20b","legacy CDN marker removed without signing server");
            Call("ModServices","LoadIndependentLinkConfig");
            var config=(JObject)app.GetType("PCL.ModSecret").GetField("ServerConfig").GetValue(null);
            Check((int)config["Link"]["EasyTierVersion"]==20604,"local EasyTier configuration loads without server");
            Check(((string)config["Link"]["Hashes"]["x86_64"]).Length==64,"EasyTier download checksum is pinned");
            string device=(string)Call("ModSecret","GetIdentify");
            Check(device.Length==32 && device==(string)Call("ModSecret","GetIdentify"),"device identifier persists per installation");
            try { app.GetType("PCL.ModSecret").GetProperty("OAuthClientId").GetValue(null); throw new Exception("missing ID was accepted"); }
            catch(TargetInvocationException e) { Check(e.InnerException.Message.Contains("Client ID"),"missing Microsoft ID gives configuration guidance"); }
            if(args.Contains("--network")) {
                Exception failure=null;
                var frame=new System.Windows.Threading.DispatcherFrame();
                var dispatcher=System.Windows.Threading.Dispatcher.CurrentDispatcher;
                var worker=new System.Threading.Thread(()=>{try {Network(core);} catch(Exception e){failure=e;} finally {dispatcher.BeginInvoke(new Action(()=>frame.Continue=false));}});
                worker.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame); worker.Join();
                if(failure!=null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            Console.WriteLine("RESULT: "+passed+" passed");
            Environment.Exit(0); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); Environment.Exit(1); return 1; }
    }
    static int PaletteField(object palette,string name) {return (int)palette.GetType().GetField(name).GetValue(palette);}
    static void Personalization() {
        for(int id=0;id<=14;id++) {
            var p=Call("ModPersonalization","GetPersonalizationPalette",id);
            Check(PaletteField(p,"Hue")==210 && PaletteField(p,"Saturation")==85 && PaletteField(p,"Gradient")==0,"Windows palette remains blue for saved id "+id);
        }
        var custom=Call("ModPersonalization","GetPersonalizationPalette",14,359,91,35,60);
        Check(PaletteField(custom,"Hue")==210 && PaletteField(custom,"Gradient")==0,"custom hue cannot re-enable sponsor-like themes");
        Call("Settings","Set","UiLauncherTheme",14);
        var secret=app.GetType("PCL.ModSecret");
        Check((int)secret.GetField("ColorHue").GetValue(null)==210 && (int)Call("Settings","Get","UiLauncherTheme")==0,"old custom setting migrates to blue");
        Check(Convert.ToInt32(secret.GetProperty("CurrentRank").GetValue(null))==0,"official sponsor identity remains None");
        var page=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.PageSetupUI"));
        Call("ModPersonalization","ApplyPersonalizationControls",page);
        Check(((System.Windows.UIElement)page.FindName("PanLauncherTheme")).Visibility==System.Windows.Visibility.Collapsed,"sponsor-like theme controls are hidden");
        Check(!((System.Windows.UIElement)page.FindName("RadioLauncherTheme14")).IsEnabled,"custom hue control cannot be selected");
        Check(page.FindName("CardBackground")!=null && page.FindName("CardMusic")!=null,"original background and music controls retained");
        Call("SettingService","RefreshSettings",page);
        page.Measure(new System.Windows.Size(950,1300)); page.Arrange(new System.Windows.Rect(0,0,950,1300));page.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(950,1300,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(page);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using(var preview=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"personalization-preview.png"))) encoder.Save(preview);
    }
    static void CustomConfig() {
        Check((string)Call("ModServices","ResolveServiceUrl","https://bmclapi2.bangbang93.com/maven/a.jar?x=1")=="https://bmcl.example.test/base/maven/a.jar?x=1","custom BMCL endpoint preserves path");
        Check((string)Call("ModDownload","DlSourceModGet","https://staging-api.modrinth.com/v2/search?q=a")=="https://mirror.example.test/modrinth/v2/search?q=a","staging API maps to correct mirror path");
        Check((string)Call("ModDownload","DlSourceModGet","https://api.curseforge.com/v1/mods/1")=="https://mirror.example.test/curseforge/v1/mods/1","CurseForge mirror preserves API route");
        Check((string)Call("ModSecret","SecretCdnSign","https://api.curseforge.com/v1/mods/1")=="https://cf.example.test/api/v1/mods/1","HTTP routing uses configured CurseForge API");
        Check(HasKey("https://cf.example.test/api/v1/mods/1"),"configured API receives local key");
        Check(!HasKey("https://cf.example.test/unrelated/v1/mods/1"),"unrelated path on same host does not receive key");
        Check((string)app.GetType("PCL.ModSecret").GetProperty("OAuthClientId").GetValue(null)=="local-test-client-id","Microsoft ID loads from local configuration");
        Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID","environment-test-id");
        Check((string)app.GetType("PCL.ModSecret").GetProperty("OAuthClientId").GetValue(null)=="environment-test-id","environment configuration takes precedence");
        Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID",null);
        string exported=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"exported-config.json");
        Call("ModServices","ExportPublicServiceConfig",exported);
        var json=JObject.Parse(File.ReadAllText(exported));
        Check(json["CurseForgeApiKey"]==null && json["UnrelatedSecret"]==null && (string)json["MicrosoftClientId"]=="local-test-client-id","modpack export uses public allowlist and excludes secrets");
    }
    static void V5Checks() {
        var secret=app.GetType("PCL.ModMicrosoftAuth");
        string device=(string)secret.GetField("MicrosoftDeviceEndpoint").GetRawConstantValue();
        string token=(string)secret.GetField("MicrosoftTokenEndpoint").GetRawConstantValue();
        Check(device.StartsWith("https://login.microsoftonline.com/consumers/") && token.StartsWith("https://login.microsoftonline.com/consumers/"),"initial and refresh authorization use the same authority");
        Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID","11111111-1111-1111-1111-111111111111");
        Check(((string)Call("ModMicrosoftAuth","MicrosoftForm","device")).Contains("XboxLive.signin+offline_access"),"device grant requests only Xbox sign-in and refresh permissions");
        string encoded=(string)Call("ModMicrosoftAuth","MicrosoftForm","refresh","a&x=1+secret");
        Check(encoded.Contains("refresh_token=a%26x%3D1%2Bsecret"),"refresh token is form encoded");
        Check((int)Call("ModMicrosoftAuth","DevicePollingDelay",7,"authorization_pending")==7,"pending response preserves server polling interval");
        Check((int)Call("ModMicrosoftAuth","DevicePollingDelay",7,"slow_down")==12,"slow_down adds five seconds");
        Check((int)Call("ModMicrosoftAuth","DevicePollingDelay",0,"pending")==1,"invalid polling interval cannot produce a busy loop");
        Check((bool)Call("ModMicrosoftAuth","IsMicrosoftVerificationUri","https://www.microsoft.com/link"),"official Microsoft verification URL accepted");
        foreach(string u in new[]{"http://microsoft.com/link","https://microsoft.com.evil.test/link","https://microsoft.com@evil.test/","https://login.microsoftonline.com:444/link"})
            Check(!(bool)Call("ModMicrosoftAuth","IsMicrosoftVerificationUri",u),"untrusted verification URL blocked: "+u);
        Check(((string)Call("ModMicrosoftAuth","MinecraftLoginFailure",403,"Invalid app registration")).Contains("Client ID"),"unapproved application is distinguished from IP errors");
        Check(((string)Call("ModMicrosoftAuth","MinecraftLoginFailure",429,"")).Contains("频繁"),"rate limit gives retry guidance");
        Call("ModNet","ShouldValidateSslCertificateOnLogin",false);
        Check(!System.Net.ServicePointManager.ServerCertificateValidationCallback(null,null,null,System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors),"migrated disabled certificate setting cannot bypass HTTPS checks");
        Check(System.Net.ServicePointManager.ServerCertificateValidationCallback(null,null,null,System.Net.Security.SslPolicyErrors.None),"valid HTTPS certificates accepted");
        string plain="synthetic-token-never-a-real-account";
        string encrypted=(string)Call("ModMicrosoftAuth","ProtectLocal",plain);
        Check(encrypted.StartsWith("dpapi:") && !encrypted.Contains(plain),"local credentials encrypted with Windows DPAPI");
        Check((string)Call("ModMicrosoftAuth","UnprotectLocal",encrypted)==plain,"DPAPI credentials round trip");
        try {Call("ModMicrosoftAuth","UnprotectLocal","dpapi:AA=="); throw new Exception("corrupt credential accepted");} catch(System.Security.Cryptography.CryptographicException) {Check(true,"corrupt ciphertext rejected");}
        Check((string)Call("ModExtraSources","GitHubSearchAddress","CaffeineMC/sodium")=="https://api.github.com/repos/CaffeineMC/sodium","GitHub direct repository lookup preserves ownership");
        Check(((string)Call("ModExtraSources","FtbSearchAddress","a&x=1")).EndsWith("term=a%26x%3D1"),"FTB query cannot inject additional URL parameters");
        string asset="https://github.com/owner/repo/releases/download/1.0/mod.jar";
        Check((bool)Call("ModExtraSources","IsAllowedGitHubAsset",asset,"owner/repo","mod.jar"),"GitHub release JAR download accepted");
        Check(!(bool)Call("ModExtraSources","IsAllowedGitHubAsset",asset.Replace("github.com","github.com.evil.test"),"owner/repo","mod.jar"),"foreign download host rejected");
        Check(!(bool)Call("ModExtraSources","IsAllowedGitHubAsset",asset,"other/repo","mod.jar"),"asset must belong to selected repository");
        Check(!(bool)Call("ModExtraSources","IsAllowedGitHubAsset",asset,"owner/repo","../mod.jar"),"release filename cannot traverse directories");
        Check(!(bool)Call("ModExtraSources","IsAllowedGitHubAsset",asset,"owner/repo","installer.exe"),"EXE assets excluded from resource browser");
        using(var request=new HttpRequestMessage(HttpMethod.Get,"https://api.curseforge.com/v1/mods/1")) {
            request.Headers.Add("x-api-key","synthetic-key");
            Check((bool)Call("ModMicrosoftAuth","NeedsPrivateTransport",request),"requests with API keys avoid shared disk cache and implicit redirects");
            using(var redirected=(HttpRequestMessage)Call("ModMicrosoftAuth","SafeRedirectGet",request,new Uri("https://files.example.test/mod.jar")))
                Check(!redirected.Headers.Contains("x-api-key"),"redirect cannot forward API key to a foreign host");
            request.Headers.Add("Authorization","Bearer synthetic-token");
            try {Call("ModMicrosoftAuth","SafeRedirectGet",request,new Uri("https://files.example.test/mod.jar"));throw new Exception("credential redirect accepted");}
            catch(HttpRequestException) {Check(true,"account-authenticated redirect rejected");}
        }
        var page=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.PageSetupUI"));
        Check(page.FindName("ComboDynamicMode")==null && page.FindName("BtnAccountManager")!=null,"actual settings page retains account management and excludes restricted effects");
        var resources=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.PageResource"));
        Check(resources.FindName("BtnGitHubSource")!=null && resources.FindName("BtnFtbSource")!=null,"actual resource page exposes new sources");
        // Redirect registry-backed account settings to temporary normal settings in this process only.
        // The real launcher registry entries and the user's account files are never accessed by these tests.
        var entries=(System.Collections.IDictionary)app.GetType("PCL.Settings").GetField("Entries").GetValue(null);
        foreach(System.Collections.DictionaryEntry e in entries) {
            string key=(string)e.Key;
            if(key.StartsWith("CacheMs") || key=="LoginMsJson") {
                var field=e.Value.GetType().GetField("Source"); field.SetValue(e.Value,Enum.ToObject(field.FieldType,0));
            }
        }
        string store=(string)app.GetType("PCL.ModAccounts").GetField("AccountStorePath").GetValue(null);
        Check(!File.Exists(store),"account test store belongs to this fresh temporary installation");
        try {
            Action<string,string> save=(uuid,name)=> {
                Call("Settings","Set","CacheMsV2Uuid",uuid); Call("Settings","Set","CacheMsV2Name",name);
                Call("Settings","Set","CacheMsV2OAuthRefresh","synthetic-refresh-"+uuid);
                Call("Settings","Set","CacheMsV2Access","synthetic-access-"+uuid);
                Call("Settings","Set","CacheMsV2ClientId","11111111-1111-1111-1111-111111111111");
                Call("Settings","Set","CacheMsV2Expires",123456789L); Call("ModAccounts","SaveCurrentAccount");
            };
            string a=new string('a',32),b=new string('b',32),client="11111111-1111-1111-1111-111111111111";
            save(a,"SyntheticAlice"); save(b,"SyntheticBob");
            Check(((JArray)Call("ModAccounts","ReadAccounts")).Count==2,"two accounts persist independently");
            string disk=File.ReadAllText(store);
            Check(disk.StartsWith("dpapi:") && !disk.Contains("SyntheticAlice") && !disk.Contains("synthetic-refresh"),"account store contains no plaintext names or tokens");
            Call("ModAccounts","SelectStoredAccount",a,client);
            Check((string)Call("Settings","Get","CacheMsV2Name")=="SyntheticAlice","switching restores selected account");
            Check((string)Call("Settings","Get","CacheMsV2OAuthRefresh")=="synthetic-refresh-"+a,"switching keeps each account's refresh token");
            save(a,"SyntheticAliceRenamed");
            Check(((JArray)Call("ModAccounts","ReadAccounts")).Count==2,"renaming a player does not duplicate its UUID");
            Call("ModAccounts","RemoveStoredAccount",b,client);
            Check(((JArray)Call("ModAccounts","ReadAccounts")).Count==1 && (string)Call("Settings","Get","CacheMsV2Name")=="SyntheticAliceRenamed","removing another account preserves active account");
            Call("ModAccounts","RemoveStoredAccount",a,client);
            Check(((JArray)Call("ModAccounts","ReadAccounts")).Count==0 && (string)Call("Settings","Get","CacheMsV2OAuthRefresh")=="","removing active account clears local active credentials");
            File.WriteAllText(store,"dpapi:AA==");
            try {Call("ModAccounts","ReadAccounts");throw new Exception("bad account file accepted");} catch(Exception e) {Check(e.Message.Contains("原文件已保留") && File.ReadAllText(store)=="dpapi:AA==","unreadable account store is preserved");}
        } finally {if(File.Exists(store)) File.Delete(store);}
        Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID",null);
    }
    static void Network(Assembly core) {
        Console.WriteLine("NETWORK: checking logger");
        core.GetType("MeloongCore.Logger").GetMethods().First(m=>m.Name=="Info" && m.GetParameters()[0].ParameterType==typeof(string)).Invoke(null,new object[]{"Network verification",Enum.ToObject(core.GetType("MeloongCore.LogBehavior"),0),"SmokeTests.cs"});
        Console.WriteLine("NETWORK: checking direct HTTP");
        Check((int)Call("ModNet","FindFreePort")>0,"network module initializes");
        var direct=(string)Call("ModNet","NetRequestByClient","https://api.modrinth.com/v2/search?query=sodium&limit=2");
        Check(JObject.Parse(direct)["hits"].Any(),"launcher HTTP client returns JSON");
        Console.WriteLine("NETWORK: checking resource request");
        var result=(JObject)Call("ModDownload","DlModRequest","https://api.modrinth.com/v2/search?query=sodium&limit=2");
        Check(result["hits"].Any(),"actual launcher Modrinth search returns projects");
        var versions=(JArray)Call("ModDownload","DlModRequest","https://api.modrinth.com/v2/project/sodium/version");
        var file=versions[0]["files"].First(f=>(bool)f["primary"]);
        Download(core,(string)file["url"],"verified-mod.jar",(string)file["hashes"]["sha1"],(long)file["size"]);
        var manifest=JObject.Parse((string)Call("ModNet","NetRequestByClientRetry","https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"));
        var version=manifest["versions"].First(v=>(string)v["id"]=="1.0");
        var metadata=JObject.Parse((string)Call("ModNet","NetRequestByClientRetry",(string)version["url"]));
        var client=metadata["downloads"]["client"];
        Download(core,(string)client["url"],"verified-minecraft-1.0.jar",(string)client["sha1"],(long)client["size"]);
    }
    static void Download(Assembly core,string url,string name,string hash,long size) {
        var checkerType=core.GetType("MeloongCore.FileChecker");
        var checker=Activator.CreateInstance(checkerType);
        checkerType.GetField("Hash").SetValue(checker,hash);
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name);
        Call("ModNet","NetDownloadByLoader",url,path,null,checker,false);
        string actual;
        using(var stream=File.OpenRead(path)) using(var sha=System.Security.Cryptography.SHA1.Create())
            actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        Check(new FileInfo(path).Length==size && actual==hash,"actual launcher download engine verifies "+name);
    }
}
