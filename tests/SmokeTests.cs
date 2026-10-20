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
            var palette=Call("ModPersonalization","GetPersonalizationPalette",id);
            Check(PaletteField(palette,"Hue")>=0 && PaletteField(palette,"Hue")<360 && PaletteField(palette,"Saturation")>=0 && PaletteField(palette,"Saturation")<=100,"valid independent palette "+id);
        }
        var custom=Call("ModPersonalization","GetPersonalizationPalette",14,359,91,35,60);
        Check(PaletteField(custom,"Hue")==359 && PaletteField(custom,"Saturation")==91 && PaletteField(custom,"LightAdjust")==15 && PaletteField(custom,"Gradient")==-30,"custom controls map to palette values");
        var limits=Call("ModPersonalization","GetPersonalizationPalette",14,999,-5,99,999);
        Check(PaletteField(limits,"Hue")==0 && PaletteField(limits,"Saturation")==0 && PaletteField(limits,"LightAdjust")==20 && PaletteField(limits,"Gradient")==90,"custom values are clamped");
        Check(PaletteField(Call("ModPersonalization","GetPersonalizationPalette",14),"Gradient")==0,"default gradient slider is centered at zero");
        Check(PaletteField(Call("ModPersonalization","GetPersonalizationPalette",-1),"Hue")==210,"unknown palette falls back to blue");
        Check(Convert.ToInt32(app.GetType("PCL.ModSecret").GetProperty("CurrentRank").GetValue(null))==0,"official sponsor identity remains None");
        Call("Settings","Set","UiLauncherTheme",14);
        Call("Settings","Set","UiLauncherHue",312);
        Call("Settings","Set","UiLauncherSat",70);
        Call("Settings","Set","UiLauncherLight",25);
        Call("Settings","Set","UiLauncherDelta",30);
        Call("ModSecret","ThemeRefresh");
        var secret=app.GetType("PCL.ModSecret");
        Check((int)secret.GetField("ColorHue").GetValue(null)==312 && (int)secret.GetField("ColorLightAdjust").GetValue(null)==5,"saved custom settings refresh runtime palette");
        Check(System.Windows.Application.Current.Resources["ColorBrush3"] is System.Windows.Media.SolidColorBrush,"theme writes actual WPF color resources");
        var page=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.PageSetupUI"));
        Call("ModPersonalization","ApplyPersonalizationControls",page);
        for(int id=0;id<=14;id++) {
            var control=(System.Windows.UIElement)page.FindName("RadioLauncherTheme"+id);
            Check(control!=null && control.IsEnabled && control.Opacity==1,"preset is selectable without sponsor code "+id);
        }
        Check(((System.Windows.UIElement)page.FindName("PanLauncherHide")).Visibility==System.Windows.Visibility.Collapsed,"sponsor overlay does not block third-party controls");
        foreach(string name in new[]{"LabLauncherTheme5Unlock","LabLauncherTheme8Copy","LabLauncherTheme9Copy","LabLauncherTheme11Click"})
            Check(((System.Windows.UIElement)page.FindName(name)).Visibility==System.Windows.Visibility.Collapsed,"legacy overlay hidden: "+name);
        Call("SettingService","RefreshSettings",page);
        page.Measure(new System.Windows.Size(950,780));
        page.Arrange(new System.Windows.Rect(0,0,950,780));
        page.UpdateLayout();
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap(950,780,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        image.Render(page);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using(var preview=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"personalization-preview.png"))) encoder.Save(preview);
        Call("Settings","Set","UiLauncherTheme",0);
        Check((int)secret.GetField("ColorHue").GetValue(null)==210,"switching back to default resets hue");
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
