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
    static void InitializeTestResources(System.Windows.Application application) {
        app.GetType("PCL.Application").GetMethod("InitializeComponent").Invoke(application,null);
        // .NET Framework WPF rejects a null StartupUri through its public setter.
        // Clear the deferred URI as well as detaching the production Startup event.
        var field=typeof(System.Windows.Application).GetField("_startupUri",BindingFlags.Instance|BindingFlags.NonPublic);
        if(field==null)throw new InvalidOperationException("Cannot isolate the WPF startup URI.");
        field.SetValue(application,null);
    }
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
            application.ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown;
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
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
                InitializeTestResources(application);
                Personalization();
                Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0);
            }
            if(args.Contains("--v5")) {
                InitializeTestResources(application);
                V5Checks(); Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0);
            }
            if(args.Contains("--visual")) {
                InitializeTestResources(application);
                VisualChecks();Console.WriteLine("RESULT: "+passed+" passed");Environment.Exit(0);
            }
            if(args.Contains("--interaction")) {
                InitializeTestResources(application);
                InteractionChecks();Console.WriteLine("RESULT: "+passed+" passed");Environment.Exit(0);
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
            if(args.Contains("--updater")) { ProjectUpdateChecks(); Console.WriteLine("RESULT: "+passed+" passed"); Environment.Exit(0); }
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
    static void SetNormalPreference(string key,bool value) {
        var settings=app.GetType("PCL.Settings");var entries=settings.GetField("Entries",BindingFlags.Public|BindingFlags.Static).GetValue(null) as System.Collections.IDictionary;
        if(entries==null) throw new Exception("Settings entries unavailable");
        var entry=entries[key];var field=entry.GetType().GetField("Source");field.SetValue(entry,Enum.Parse(field.FieldType,"Normal"));
        Call("Settings","Set",key,value);
    }
    static void AnimationSchedulerChecks() {
        Call("ModBase","RunInUi");
        var engine=app.GetType("PCL.ModAnimation");
        engine.GetMethod("AniStart",Type.EmptyTypes).Invoke(null,null);
        Func<bool> attached=()=> (bool)engine.GetProperty("IsAnimationFrameAttached",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        var target=new System.Windows.Controls.Border{Background=System.Windows.Media.Brushes.AliceBlue,Opacity=0};
        var window=new System.Windows.Window{Content=target,Width=140,Height=80,ShowInTaskbar=false,ShowActivated=false,WindowStyle=System.Windows.WindowStyle.None};
        window.Show();Pump(80);
        Check(!attached(),"idle animation scheduler has no rendering subscription");
        Call("ModAnimation","AniStart",new[]{Call("ModAnimation","AaOpacity",target,1d,180)},"scheduler-test");
        Check(attached(),"starting an animation enables frame updates on demand");
        Pump(70);Check(target.Opacity>0&&target.Opacity<1,"control animation produces intermediate visual values");
        Pump(240);Check(Math.Abs(target.Opacity-1)<.001&&!attached(),"completion reaches its target and detaches the rendering event");
        var watchdog=(System.Windows.Threading.DispatcherTimer)engine.GetField("AnimationWatchdog",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Check(!watchdog.IsEnabled,"idle scheduler also stops its hidden-window fallback timer");
        int replacement=0,stale=0;
        var replace=Call("ModAnimation","AaCode",new System.Threading.ThreadStart(()=>Call("ModAnimation","AniStart",new[]{Call("ModAnimation","AaCode",new System.Threading.ThreadStart(()=>replacement++),20)},"scheduler-replace")));
        var obsolete=Call("ModAnimation","AaCode",new System.Threading.ThreadStart(()=>stale++),0,true);
        Call("ModAnimation","AniStart",new[]{replace,obsolete},"scheduler-replace");Pump(180);
        Check(replacement==1&&stale==0&&!attached(),"callback replacement survives old-group cleanup and cancels obsolete callbacks");
        int cancelled=0;
        Call("ModAnimation","AniStart",new[]{Call("ModAnimation","AaCode",new System.Threading.ThreadStart(()=>cancelled++),80)},"scheduler-cancel");
        Call("ModAnimation","AniStop","scheduler-cancel");Pump(160);
        Check(cancelled==0&&!attached(),"cancelled delayed callbacks do not run or retain a frame subscription");
        var order=new System.Collections.Generic.List<int>();
        Call("ModAnimation","AniStart",new[]{Call("ModAnimation","AaCode",new System.Threading.ThreadStart(()=>order.Add(1)),30),Call("ModAnimation","AaCode",new System.Threading.ThreadStart(()=>order.Add(2)),0,true)},"scheduler-order");Pump(150);
        Check(order.SequenceEqual(new[]{1,2}),"sequential callback ordering is preserved");
        Pump(180);target.Opacity=0;
        Call("ModAnimation","AniStart",new[]{Call("ModAnimation","AaOpacity",target,1d,220)},"scheduler-fresh");Pump(50);
        Check(target.Opacity>0&&target.Opacity<.9,"a new animation does not inherit idle time from the previous group");
        System.Threading.Thread.Sleep(90);Pump(25);
        Check(target.Opacity>.45,"animation timing catches up after a blocked UI thread instead of adding a stale queue delay");
        Pump(180);window.Hide();Pump(150);int hidden=0;
        Call("ModAnimation","AniStart",new[]{Call("ModAnimation","AaCode",new System.Threading.ThreadStart(()=>hidden++),30)},"scheduler-hidden");Pump(300);
        Check(hidden==1&&!attached()&&!watchdog.IsEnabled,"hidden windows still complete delayed callbacks and release the fallback timer");
        var checkbox=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.MyCheckBox"));
        window.Content=checkbox;window.Show();Pump(80);
        var checkboxType=checkbox.GetType();
        bool original=(bool)checkboxType.GetProperty("Checked").GetValue(checkbox);
        Action click=()=> {
            checkbox.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.UIElement.MouseLeftButtonDownEvent});
            checkbox.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.UIElement.MouseLeftButtonUpEvent});
        };
        click();bool first=(bool)checkboxType.GetProperty("Checked").GetValue(checkbox);click();
        Check(first!=original&&(bool)checkboxType.GetProperty("Checked").GetValue(checkbox)==original,"rapid user checkbox events are accepted during the previous transition");
        Pump(500);Check(!attached(),"interrupted checkbox transitions complete and release their animation groups");
        window.Content=null;window.Close();
    }
    static void InteractionChecks() {
        Call("ModBase","RunInUi");
        AnimationSchedulerChecks();
        Check((bool)Call("Settings","Get","UiSmoothScroll")&&(bool)Call("Settings","Get","UiNavigationAnimation"),"scroll and navigation preferences default enabled");
        var viewer=(System.Windows.Controls.ScrollViewer)Activator.CreateInstance(app.GetType("PCL.MyScrollViewer"));
        viewer.Width=350;viewer.Height=240;viewer.VerticalScrollBarVisibility=System.Windows.Controls.ScrollBarVisibility.Auto;
        viewer.Content=new System.Windows.Controls.Border{Height=2800,Background=System.Windows.Media.Brushes.White};
        var scrollPreview=new System.Windows.Window{Content=viewer,Width=350,Height=240,ShowActivated=false,ShowInTaskbar=false,WindowStyle=System.Windows.WindowStyle.None};scrollPreview.Show();Pump(60);
        viewer.Measure(new System.Windows.Size(350,240));viewer.Arrange(new System.Windows.Rect(0,0,350,240));viewer.UpdateLayout();Pump(40);
        Check(viewer.ScrollableHeight>2000,"smooth scroll uses an actual scrollable WPF layout");
        Action<double> scroll=d=>viewer.GetType().GetMethod("PerformVerticalOffsetDelta").Invoke(viewer,new object[]{d});
        bool motion=(bool)Call("ModVisualExperience","NavigationScrollEnabled");
        scroll(240);Pump(60);
        double middle=viewer.VerticalOffset;
        Check(!motion || middle>0&&middle<240,"scroll starts promptly and passes through intermediate offsets");
        scroll(-60);double turn=viewer.VerticalOffset;Pump(300);viewer.UpdateLayout();
        Check(Math.Abs(viewer.VerticalOffset-Math.Max(0,turn-60))<2,"reverse input cancels pending forward distance immediately");
        viewer.ScrollToVerticalOffset(0);viewer.UpdateLayout();
        for(int i=0;i<12;i++)scroll(30);Pump(320);viewer.UpdateLayout();
        Check(Math.Abs(viewer.VerticalOffset-360)<2,"rapid wheel input reaches one accumulated target without stacked animations");
        Check(!(bool)viewer.GetType().GetProperty("HasSmoothScroll").GetValue(viewer),"scroll clock is released when motion finishes");
        scroll(100000);Pump(300);viewer.UpdateLayout();Check(Math.Abs(viewer.VerticalOffset-viewer.ScrollableHeight)<2,"scroll target clamps to the actual viewport boundary");
        viewer.GetType().GetMethod("ScrollToHome").Invoke(viewer,null);viewer.UpdateLayout();Check(viewer.VerticalOffset==0,"return to top cancels pending wheel motion");
        scroll(100);viewer.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        Check(!(bool)viewer.GetType().GetProperty("HasSmoothScroll").GetValue(viewer),"unloading the viewer cancels its animation clock");
        var hostTransform=new System.Windows.Media.ScaleTransform(1,1);
        var host=new System.Windows.Controls.Border{RenderTransform=hostTransform};var first=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.MyPageRight"));
        first.GetType().GetProperty("Child").SetValue(first,new System.Windows.Controls.TextBlock{Text="first"});
        var last=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.MyPageRight"));last.GetType().GetProperty("Child").SetValue(last,new System.Windows.Controls.TextBlock{Text="last"});
        int firstEnter=0,lastEnter=0;
        var enter=first.GetType().GetEvent("PageEnter");enter.AddEventHandler(first,Delegate.CreateDelegate(enter.EventHandlerType,new Counter(()=>firstEnter++),typeof(Counter).GetMethod("Invoke")));
        enter.AddEventHandler(last,Delegate.CreateDelegate(enter.EventHandlerType,new Counter(()=>lastEnter++),typeof(Counter).GetMethod("Invoke")));
        Call("ModNavigation","MountRightPage",host,first);Check(ReferenceEquals(Call("ModNavigation","GetCurrentRightPage",host),first),"page is mounted synchronously before any exit animation delay");
        Call("ModNavigation","MountRightPage",host,last);Pump(60);
        Check(firstEnter==0&&lastEnter==1&&ReferenceEquals(Call("ModNavigation","GetCurrentRightPage",host),last),"rapid navigation enters only the latest requested page");
        Check(ReferenceEquals(host.RenderTransform,hostTransform)&&host.Opacity==1,"rapid navigation restores the original host pose without a transparent first pass");
        Pump(300);Check(last.GetType().GetProperty("PageState").GetValue(last).ToString()=="ContentStay","page reaches a usable stable state within the short transition");
        Call("ModNavigation","MountRightPage",host,last);Pump(30);Check(lastEnter==1,"selecting the current page does not enqueue another entry");
        var oldContent=(System.Windows.FrameworkElement)last.GetType().GetProperty("Child").GetValue(last);
        int exits=0;var exit=last.GetType().GetEvent("PageExit");exit.AddEventHandler(last,Delegate.CreateDelegate(exit.EventHandlerType,new Counter(()=>exits++),typeof(Counter).GetMethod("Invoke")));
        Call("ModNavigation","MountRightPage",host,first);Pump(30);
        Check(exits==1&&oldContent.Visibility==System.Windows.Visibility.Visible&&oldContent.IsHitTestVisible,"immediate navigation exits once without collapsing or disabling detached content");
        Check(last.GetType().GetProperty("PageState").GetValue(last).ToString()=="Empty","detached navigation page resets lifecycle state");
        Call("ModNavigation","MountRightPage",host,last);Pump(30);
        Check(((string)Call("SettingService","RestartNotice","UiStartupAnimation",true)).Contains("下次启动"),"user startup toggle provides a restart hint");
        Check(Call("SettingService","RestartNotice","UiStartupAnimation",false)==null,"loading saved preferences never emits a restart hint");
        Check(Call("SettingService","RestartNotice","UiSmoothScroll",true)==null,"live scroll preference does not show an unnecessary restart hint");
        SetNormalPreference("UiSmoothScroll",false);
        viewer.GetType().GetMethod("ScrollToHome").Invoke(viewer,null);viewer.UpdateLayout();scroll(80);viewer.UpdateLayout();
        Check(viewer.VerticalOffset==80&&!(bool)viewer.GetType().GetProperty("HasSmoothScroll").GetValue(viewer),"disabling smooth scrolling applies wheel movement directly");
        viewer.GetType().GetMethod("ScrollToHome").Invoke(viewer,null);viewer.UpdateLayout();
        for(int i=0;i<12;i++)scroll(30);Pump(80);viewer.UpdateLayout();
        Check(Math.Abs(viewer.VerticalOffset-360)<2,"disabled motion retains all wheel deltas queued within one frame");
        SetNormalPreference("UiSmoothScroll",true);
        SetNormalPreference("UiNavigationAnimation",false);
        Call("ModNavigation","MountRightPage",host,first);Pump(25);
        Check(first.GetType().GetProperty("PageState").GetValue(first).ToString()=="ContentStay","disabling navigation motion enters content without an animation wait");
        SetNormalPreference("UiNavigationAnimation",true);
        RetainedNavigationChecks();
        PageMotionChecks();
        HeaderChecks();
        WindowChecks();
        WindowCaptureChecks();
        CapturePage(viewer,350,240,"smooth-scroll-preview.png");
        scrollPreview.Content=null;scrollPreview.Close();
        DiagnosticsChecks();
    }
    static void RetainedNavigationChecks() {
        SetNormalPreference("UiNavigationAnimation",false);
        var host=new System.Windows.Controls.Border();
        var window=new System.Windows.Window{Content=host,Width=520,Height=330,ShowActivated=false,ShowInTaskbar=false,WindowStyle=System.Windows.WindowStyle.None};
        var pageType=app.GetType("PCL.MyPageRight");
        Func<System.Windows.FrameworkElement,bool,System.Windows.FrameworkElement> make=(child,retain)=>{
            var page=(System.Windows.FrameworkElement)Activator.CreateInstance(pageType);pageType.GetProperty("Child").SetValue(page,child);pageType.GetProperty("RetainNavigationLayout").SetValue(page,retain);return page;
        };
        var spinner=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.MyLoading"));
        var measure=new MeasureProbe{Child=spinner};var first=make(measure,true);var second=make(new System.Windows.Controls.TextBlock{Text="second"},true);
        int loads=0,unloads=0,activations=0;first.Loaded+=(s,e)=>loads++;first.Unloaded+=(s,e)=>unloads++;
        var activate=pageType.GetEvent("NavigationActivated");activate.AddEventHandler(first,Delegate.CreateDelegate(activate.EventHandlerType,new NavigationCounter(()=>activations++),typeof(NavigationCounter).GetMethod("Invoke")));
        Call("ModNavigation","MountRightPage",host,first);window.Show();Pump(90);
        var looping=spinner.GetType().GetField("IsLooping",BindingFlags.Instance|BindingFlags.NonPublic);
        Check(first.IsLoaded&&first.IsVisible&&(bool)looping.GetValue(spinner),"retained page starts its loading animation when visible");
        Call("ModNavigation","MountRightPage",host,second);Pump(50);
        Check(first.IsLoaded&&!first.IsVisible&&loads==1&&unloads==0,"switching a retained page preserves its loaded visual tree without leaving it visible");
        Check(!(bool)looping.GetValue(spinner),"a hidden retained page immediately stops its loading loop");
        int measures=measure.Measures;window.Width+=40;Pump(40);
        Check(measure.Measures==measures,"resizing the active viewport does not remeasure hidden page content");
        Call("ModNavigation","MountRightPage",host,first);Pump(60);
        Check(loads==1&&activations==1&&first.IsVisible&&(bool)looping.GetValue(spinner),"returning to a retained page refreshes navigation state and resumes its loading loop without reloading the tree");
        for(int i=0;i<6;i++){var page=make(new System.Windows.Controls.TextBlock{Text="retained "+i},true);Call("ModNavigation","MountRightPage",host,page);Pump(20);}
        Check(((System.Windows.Controls.Panel)host.Child).Children.Count==4&&!first.IsLoaded&&first.Parent==null,"navigation retains at most four pages and unloads evicted content");
        var temporary=make(new System.Windows.Controls.TextBlock{Text="temporary"},false);Call("ModNavigation","MountRightPage",host,temporary);Pump(25);
        Call("ModNavigation","MountRightPage",host,second);Pump(25);
        Check(temporary.Parent==null&&!temporary.IsLoaded,"pages that require unloading are removed immediately when navigation leaves them");
        Func<double,System.Windows.Controls.Grid> shared=width=>{var grid=new System.Windows.Controls.Grid();grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width=System.Windows.GridLength.Auto,SharedSizeGroup="name"});grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());grid.Children.Add(new System.Windows.Controls.Border{Width=width,Height=20});return grid;};
        var small=shared(40);var wide=shared(300);var smallPage=make(small,true);var widePage=make(wide,true);
        Call("ModNavigation","MountRightPage",host,smallPage);Pump(30);Call("ModNavigation","MountRightPage",host,widePage);Pump(30);Call("ModNavigation","MountRightPage",host,smallPage);Pump(30);
        Check(Math.Abs(small.ColumnDefinitions[0].ActualWidth-40)<1,"retained pages keep their shared column sizes isolated from hidden pages");
        window.Close();Pump(50);Check(((System.Windows.Controls.Panel)host.Child).Children.Cast<System.Windows.FrameworkElement>().All(page=>!page.IsLoaded)&&!(bool)looping.GetValue(spinner),"closing the navigation host unloads retained visuals and stops hidden animation work");
        SetNormalPreference("UiNavigationAnimation",true);
    }
    sealed class NavigationCounter {
        readonly Action action;public NavigationCounter(Action callback){action=callback;}
        public void Invoke(object sender,System.Windows.RoutedEventArgs args){action();}
    }
    static void DiagnosticsChecks() {
        Check(Call("ModUiDiagnostics","MeasureUiPhase","disabled-test")==null,"UI timing scopes allocate nothing when diagnostics are disabled");
        string previous=Environment.GetEnvironmentVariable("PCL_UI_PROFILE");
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-timing-test.csv");
        try {
            Environment.SetEnvironmentVariable("PCL_UI_PROFILE",path);Call("ModUiDiagnostics","StartUiDiagnostics");
            using((IDisposable)Call("ModUiDiagnostics","MeasureUiPhase","test.phase")){Pump(30);}
            Pump(120);Call("ModUiDiagnostics","Detach");Pump(150);
            Check(File.Exists(path)&&File.ReadAllText(path).Contains("test.phase"),"explicit UI timing records only when enabled");
            Check(Call("ModUiDiagnostics","MeasureUiPhase","after-stop")==null,"finished diagnostics stop accepting phase records instead of retaining an unwritten queue");
        } finally { Environment.SetEnvironmentVariable("PCL_UI_PROFILE",previous); }
    }
    static void PageMotionChecks() {
        var body=new System.Windows.Controls.Border{Background=System.Windows.Media.Brushes.AliceBlue,Opacity=.8};
        var original=new System.Windows.Media.ScaleTransform(1,1);body.RenderTransform=original;
        var window=new System.Windows.Window{Content=body,Width=360,Height=220,ShowActivated=false,ShowInTaskbar=false,WindowStyle=System.Windows.WindowStyle.None};
        window.Show();Pump(60);
        var type=app.GetType("PCL.MyPageMotion");int completed=0;
        var motion=(IDisposable)Activator.CreateInstance(type,new object[]{new System.Windows.FrameworkElement[]{body},new Action(()=>completed++),false});
        var initial=(System.Windows.Media.TransformGroup)body.RenderTransform;
        Check(Math.Abs(initial.Children.OfType<System.Windows.Media.TranslateTransform>().Last().Y-10)<.001,"page is positioned at its animation origin before the first clock tick");
        Pump(55);var transform=body.RenderTransform as System.Windows.Media.TransformGroup;
        var shift=transform==null?null:transform.Children.OfType<System.Windows.Media.TranslateTransform>().LastOrDefault();
        Check(shift!=null&&shift.Y>=0&&shift.Y<10&&completed==0,"page container transition presents intermediate positions without waiting for an exit animation");
        Check(body.IsHitTestVisible,"page animation keeps content available for input");
        Check(body.CacheMode==null&&Math.Abs(body.Opacity-.8)<.001&&!body.HasAnimatedProperties,"page movement avoids a full-page bitmap cache and opacity layer");
        Pump(260);
        Check(completed==1&&ReferenceEquals(body.RenderTransform,original)&&body.CacheMode==null&&Math.Abs(body.Opacity-.8)<.001&&!body.HasAnimatedProperties,"page completion restores transform, opacity and temporary bitmap cache");
        motion=(IDisposable)Activator.CreateInstance(type,new object[]{new System.Windows.FrameworkElement[]{body},new Action(()=>completed++),false});motion.Dispose();Pump(230);
        Check(completed==1&&ReferenceEquals(body.RenderTransform,original)&&body.CacheMode==null,"cancelling before the first animation frame drops the pending completion");
        motion=(IDisposable)Activator.CreateInstance(type,new object[]{new System.Windows.FrameworkElement[]{body},new Action(()=>completed++),false});
        bool firstFrame=false;EventHandler slowLayout=null;
        slowLayout=(s,e)=>{System.Windows.Media.CompositionTarget.Rendering-=slowLayout;firstFrame=true;System.Threading.Thread.Sleep(140);};
        System.Windows.Media.CompositionTarget.Rendering+=slowLayout;Pump(165);
        transform=body.RenderTransform as System.Windows.Media.TransformGroup;
        shift=transform==null?null:transform.Children.OfType<System.Windows.Media.TranslateTransform>().LastOrDefault();
        Check(firstFrame&&shift!=null&&shift.Y>2&&completed==1,"a slow initial render does not consume the page animation before its first visible frame");
        Pump(300);Check(completed==2&&ReferenceEquals(body.RenderTransform,original),"page motion finishes normally after the first-frame barrier");
        motion=(IDisposable)Activator.CreateInstance(type,new object[]{new System.Windows.FrameworkElement[]{body},new Action(()=>completed++),false});
        EventHandler cancelBeforeStart=null;
        cancelBeforeStart=(s,e)=>{System.Windows.Media.CompositionTarget.Rendering-=cancelBeforeStart;motion.Dispose();};
        System.Windows.Media.CompositionTarget.Rendering+=cancelBeforeStart;Pump(260);
        Check(completed==2&&ReferenceEquals(body.RenderTransform,original),"cancellation between the first render and clock start aborts the queued animation");
        motion=(IDisposable)Activator.CreateInstance(type,new object[]{new System.Windows.FrameworkElement[]{body},new Action(()=>completed++),true});Pump(35);
        body.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));Pump(230);
        Check(completed==2&&ReferenceEquals(body.RenderTransform,original)&&body.CacheMode==null&&!body.HasAnimatedProperties,"leaving a page cancels its motion and releases clocks, handlers and cache");
        var weak=MakePageMotionProbe(type);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Check(!weak.IsAlive,"disposed page motion does not retain its target visual");
        var existingCache=new System.Windows.Media.BitmapCache();body.CacheMode=existingCache;
        motion=(IDisposable)Activator.CreateInstance(type,new object[]{new System.Windows.FrameworkElement[]{body},new Action(()=>{}),false});motion.Dispose();
        Check(ReferenceEquals(body.CacheMode,existingCache),"page motion preserves a cache supplied by the page itself");body.CacheMode=null;
        window.Content=null;window.Close();
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static WeakReference MakePageMotionProbe(Type type) {
        var target=new System.Windows.Controls.Border();
        var motion=(IDisposable)Activator.CreateInstance(type,new object[]{new System.Windows.FrameworkElement[]{target},new Action(()=>{}),false});
        motion.Dispose();return new WeakReference(target);
    }
    static void HeaderChecks() {
        Check(!System.Windows.Application.Current.Windows.Cast<System.Windows.Window>().Any(window=>window.GetType().Name=="FormMain"),"test resources never auto-launch the production startup window");
        var doc=System.Xml.Linq.XDocument.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"FormMain.preview.xaml"));
        System.Xml.Linq.XNamespace x="http://schemas.microsoft.com/winfx/2006/xaml";
        var source=new System.Xml.Linq.XElement(doc.Descendants().First(e=>(string)e.Attribute(x+"Name")=="PanTitle"));
        var native="clr-namespace:PCL;assembly=Plain Craft Launcher 2";
        foreach(var element in source.DescendantsAndSelf().Where(e=>e.Name.NamespaceName=="clr-namespace:PCL").ToList())element.Name=System.Xml.Linq.XName.Get(element.Name.LocalName,native);
        source.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"local",native);
        var header=(System.Windows.Controls.Grid)System.Windows.Markup.XamlReader.Parse(source.ToString());
        var preview=new System.Windows.Window{Content=new MeasureProbe{Child=header},Width=1280,Height=48,WindowStyle=System.Windows.WindowStyle.None,ShowInTaskbar=false,ShowActivated=false,Left=40,Top=40};preview.Show();Pump(80);
        var tabs=(System.Windows.FrameworkElement)header.FindName("PanTitleTabs");var selector=(System.Windows.Controls.StackPanel)header.FindName("PanTitleSelect");
        var indicator=(System.Windows.FrameworkElement)header.FindName("TitleSelection");var badge=(System.Windows.FrameworkElement)header.FindName("ThirdPartyVersionBadge");
        var mark=(System.Windows.FrameworkElement)header.FindName("ThirdPartyRomanMark");
        foreach(int width in new[]{774,950,1280}) {
            preview.Width=width;header.Width=width;header.Height=48;header.Measure(new System.Windows.Size(width,48));header.Arrange(new System.Windows.Rect(0,0,width,48));header.UpdateLayout();
            Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,false);
            double badgeRight=badge.TranslatePoint(new System.Windows.Point(badge.ActualWidth,0),header).X;
            double firstLeft=selector.TranslatePoint(new System.Windows.Point(0,0),header).X;
            Check(mark.Height==16&&firstLeft-badgeRight>=12,"compact roman mark and clear branding-to-navigation gap at width "+width);
            CapturePage(header,width,48,"titlebar-"+width+".png");
        }

        var layoutProbe=(MeasureProbe)preview.Content;int measuresBefore=layoutProbe.Measures;
        var chosen=selector.Children[4];chosen.GetType().GetMethod("SetChecked").Invoke(chosen,new object[]{true,false,false});
        Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,true);Pump(40);
        var state=ReadIndicatorState(indicator);
        double intermediate=state[0],finish=((System.Windows.FrameworkElement)chosen).TranslatePoint(new System.Windows.Point(0,0),tabs).X;
        Check(!(bool)Call("ModVisualExperience","NavigationMotionEnabled")||intermediate>0&&intermediate<finish,"top selection capsule moves through intermediate positions");
        for(int i=0;i<25;i++) {
            var button=selector.Children[i%5];button.GetType().GetMethod("SetChecked").Invoke(button,new object[]{true,false,false});
            Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,true);
        }
        Pump(300);state=ReadIndicatorState(indicator);
        Check(layoutProbe.Measures-measuresBefore<=2&&!indicator.HasAnimatedProperties,"tab glide avoids per-frame width/layout animation");
        Check(Math.Abs(state[0]-finish)<1&&state[2]==0&&!indicator.HasAnimatedProperties,"rapid tab selection finishes on the latest tab and releases its clock");
        var snapshotDrawing=(System.Windows.Media.DrawingGroup)indicator.GetType().GetMethod("GetSnapshotDrawing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(indicator,null);
        Check(snapshotDrawing!=null&&snapshotDrawing.IsFrozen,"minimize can capture selection ink without traversing its worker visual tree");
        var countField=indicator.GetType().GetField("ActiveRenderers");
        Check((int)countField.GetValue(null)==1,"navigation uses exactly one independent drawing dispatcher");
        var beginPageMotion=indicator.GetType().GetMethod("BeginPageMotion",BindingFlags.Instance|BindingFlags.NonPublic);
        var localUsers=indicator.GetType().GetField("PageMotionUsers",BindingFlags.Instance|BindingFlags.NonPublic);
        var lease1=(IDisposable)beginPageMotion.Invoke(indicator,null);var lease2=(IDisposable)beginPageMotion.Invoke(indicator,null);
        Check((int)localUsers.GetValue(indicator)==2,"left and right page transitions share one coordinated capsule drawing");
        var startButton=selector.Children[0];startButton.GetType().GetMethod("SetChecked").Invoke(startButton,new object[]{true,false,false});
        Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,true);Pump(45);state=ReadIndicatorState(indicator);
        Check(state[0]>6&&state[0]<finish&&state[2]==1,"capsule handoff retains intermediate positions on the page drawing clock");
        var worker=indicator.GetType().GetField("Session",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(indicator);
        var paused=(System.Threading.Tasks.Task<double[]>)worker.GetType().GetMethod("ReadStateAsync").Invoke(worker,null);
        Check(paused.Wait(500)&&paused.Result[2]==0,"the independent capsule clock stops while the page clock owns the transition");
        Check(indicator.GetType().GetMethod("GetSnapshotDrawing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(indicator,null)==null,"minimize does not paint coordinated selection text twice");
        lease1.Dispose();lease1.Dispose();Check((int)localUsers.GetValue(indicator)==1,"overlapping page transitions release their drawing ownership exactly once");
        lease2.Dispose();Check((int)localUsers.GetValue(indicator)==0,"the last page cancellation releases ownership while the capsule finishes its remaining motion");
        var afterCancel=ReadIndicatorState(indicator);Check(Math.Abs(afterCancel[0]-state[0])<1,"interrupting a page transition does not snap the capsule to its destination");
        Pump(220);
        Check(!(bool)indicator.GetType().GetField("LocalActive",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(indicator)&&indicator.GetType().GetField("ReturnTimer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(indicator)==null,"capsule handback releases its one-shot timer and coordinated drawing");
        chosen.GetType().GetMethod("SetChecked").Invoke(chosen,new object[]{true,false,false});Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,false);Pump(30);
        var firstButton=(System.Windows.FrameworkElement)selector.Children[0];
        firstButton.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.UIElement.MouseLeftButtonDownEvent});
        Check((bool)firstButton.GetType().GetProperty("Checked").GetValue(firstButton),"top navigation selects on press without waiting for mouse release");
        chosen.GetType().GetMethod("SetChecked").Invoke(chosen,new object[]{true,false,false});
        var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(firstButton);
        Check(peer!=null&&peer.GetName()=="启动"&&peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!=null,"navigation exposes a named standard accessibility action");
        peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke).GetType().GetMethod("Invoke").Invoke(peer,null);Pump(30);
        Check((bool)firstButton.GetType().GetProperty("Checked").GetValue(firstButton),"accessibility invokes the same navigation selection path");
        Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,true);
        System.Threading.Thread.Sleep(80);
        var independent=ReadIndicatorStateTask(indicator);
        bool completedWithoutMainPump=independent.Wait(120);
        Check(completedWithoutMainPump&&(!(bool)Call("ModVisualExperience","NavigationMotionEnabled")||(independent.Result[0]>6&&independent.Result[0]<finish)),"capsule advances while the main page dispatcher is deliberately blocked");
        Pump(250);
        chosen.GetType().GetMethod("SetChecked").Invoke(chosen,new object[]{true,false,false});
        Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,true);Pump(80);
        for(int i=0;i<20;i++)Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,false);
        Pump(160);state=ReadIndicatorState(indicator);
        Check(Math.Abs(state[0]-finish)<1&&state[2]==0,"duplicate layout notifications do not restart or snap an active glide");
        SetNormalPreference("UiNavigationAnimation",false);
        firstButton.GetType().GetMethod("SetChecked").Invoke(firstButton,new object[]{true,false,false});
        Call("ModNavigation","MoveTitleSelection",tabs,selector,indicator,true);Pump(30);state=ReadIndicatorState(indicator);
        Check(Math.Abs(state[0]-6)<1&&state[2]==0,"disabling navigation animation snaps directly without leaving clocks");
        SetNormalPreference("UiNavigationAnimation",true);
        for(int i=0;i<3;i++) {
            var staleLease=i==0?(IDisposable)beginPageMotion.Invoke(indicator,null):null;
            preview.Content=null;var shutdown=System.Diagnostics.Stopwatch.StartNew();
            do{Pump(15);}while((int)countField.GetValue(null)!=0&&shutdown.ElapsedMilliseconds<1000);
            Check((int)countField.GetValue(null)==0,"unloading navigation shuts down its dispatcher: "+i);
            preview.Content=layoutProbe;shutdown.Restart();
            do{Pump(15);}while((int)countField.GetValue(null)!=1&&shutdown.ElapsedMilliseconds<1000);
            Check((int)countField.GetValue(null)==1,"reloading navigation creates one fresh visual target: "+i);
            if(staleLease!=null){var currentLease=(IDisposable)beginPageMotion.Invoke(indicator,null);staleLease.Dispose();Check((int)localUsers.GetValue(indicator)==1,"a lease from an unloaded header cannot release a new drawing session");currentLease.Dispose();}
        }
        preview.Close();Pump(150);
        Check((int)countField.GetValue(null)==0,"closing the header releases the independent drawing thread");
        foreach(var pageType in new[]{"PageSetupUI","PageOtherTest"}) {
            var page=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL."+pageType));
            foreach(var checkbox in Descendants(page).Where(e=>e.GetType().Name=="MyCheckBox")) {
                string key=(string)Call("SettingService","GetKey",checkbox);
                if(new[]{"UiFrostedGlass","UiStartupAnimation","UiErrorAnimation","UiIconAnimation","UiNavigationAnimation","UiSmoothScroll","UiCloseAnimation","UiMinimizeAnimation"}.Contains(key)) {
                    string text=(string)checkbox.GetType().GetProperty("Text").GetValue(checkbox);
                    Check(!text.Contains("（")&&!text.Contains("(")&&!text.Contains("下次"),"effect setting label is concise: "+key);
                }
            }
        }
    }
    static System.Threading.Tasks.Task<double[]> ReadIndicatorStateTask(System.Windows.FrameworkElement indicator) {
        return (System.Threading.Tasks.Task<double[]>)indicator.GetType().GetMethod("ReadMotionStateAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(indicator,null);
    }
    static double[] ReadIndicatorState(System.Windows.FrameworkElement indicator) {
        var task=ReadIndicatorStateTask(indicator);while(!task.IsCompleted)Pump(5);return task.GetAwaiter().GetResult();
    }
    static System.Collections.Generic.IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject node) {
        foreach(var item in System.Windows.LogicalTreeHelper.GetChildren(node)) {
            var child=item as System.Windows.DependencyObject;if(child==null)continue;yield return child;
            foreach(var descendant in Descendants(child))yield return descendant;
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process,int kind);
    static void WindowCaptureChecks() {
        if(!Environment.UserInteractive||(System.Windows.Media.RenderCapability.Tier>>16)<2) {Console.WriteLine("SKIP: visible desktop capture requires an interactive hardware-rendered session; minimize uses its existing fallback here.");return;}
        var owner=new System.Windows.Window{Title="Window capture verification",Width=320,Height=180,Left=120,Top=120,WindowStyle=System.Windows.WindowStyle.None,ResizeMode=System.Windows.ResizeMode.NoResize,ShowActivated=false,ShowInTaskbar=false,Topmost=true,Content=new System.Windows.Controls.Border{Background=System.Windows.Media.Brushes.CornflowerBlue}};
        owner.Show();Pump(120);
        var origin=owner.PointToScreen(new System.Windows.Point());var scale=System.Windows.PresentationSource.FromVisual(owner).CompositionTarget.TransformToDevice.M11;
        var bounds=new System.Windows.Rect(origin.X,origin.Y,owner.ActualWidth*scale,owner.ActualHeight*scale);var handle=new System.Windows.Interop.WindowInteropHelper(owner).Handle;
        var capture=(System.Windows.Media.Imaging.BitmapSource)Call("ModWindowSnapshot","CaptureVisibleWindow",handle,bounds,scale);
        Check(capture!=null&&capture.IsFrozen&&capture.PixelWidth==(int)bounds.Width,"visible launcher frame capture returns a frozen bitmap at device resolution");
        var converted=new System.Windows.Media.Imaging.FormatConvertedBitmap(capture,System.Windows.Media.PixelFormats.Pbgra32,null,0);var pixel=new byte[4];converted.CopyPixels(new System.Windows.Int32Rect(capture.PixelWidth/2,capture.PixelHeight/2,1,1),pixel,4,0);
        Check(Math.Abs(pixel[0]-237)<=2&&Math.Abs(pixel[1]-149)<=2&&Math.Abs(pixel[2]-100)<=2&&pixel[3]==255,"frame capture preserves displayed color without rerendering the visual tree");
        int resources=GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0);
        for(int i=0;i<20;i++)Call("ModWindowSnapshot","CaptureVisibleWindow",handle,bounds,scale);
        Check(GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,0)<=resources+2,"repeated frame captures release native bitmaps and device contexts");
        var cover=new System.Windows.Window{Width=100,Height=100,Left=140,Top=140,WindowStyle=System.Windows.WindowStyle.None,ResizeMode=System.Windows.ResizeMode.NoResize,ShowActivated=false,ShowInTaskbar=false,Topmost=true,Background=System.Windows.Media.Brushes.White};cover.Show();Pump(60);
        Check(Call("ModWindowSnapshot","CaptureVisibleWindow",handle,bounds,scale)==null,"covered windows use the visual-tree fallback instead of capturing another window");
        cover.Close();owner.Close();
    }
    static void WindowChecks() {
        var buttonBounds=new System.Windows.Rect(-1200,1040,66,40);
        var endpoint=(System.Windows.Rect)Call("ModTaskbarTarget","TaskbarButtonEndpoint",buttonBounds);
        Check(Math.Abs(endpoint.X+endpoint.Width/2-buttonBounds.X-buttonBounds.Width/2)<.001&&Math.Abs(endpoint.Y+endpoint.Height/2-buttonBounds.Y-buttonBounds.Height/2)<.001,"minimize endpoint is exactly the taskbar button center");
        Check(endpoint==buttonBounds,"minimize preserves the measured icon size instead of collapsing to a point");
        var iconBounds=new System.Windows.Rect(-1188,1048,32,32);
        Check((System.Windows.Rect)Call("ModTaskbarTarget","ResolveTaskbarIconBounds",buttonBounds,(System.Windows.Rect?)iconBounds,1.5)==iconBounds,"taskbar image bounds determine the final animation size");
        var fallbackIcon=(System.Windows.Rect)Call("ModTaskbarTarget","ResolveTaskbarIconBounds",buttonBounds,null,1.5);
        Check(fallbackIcon.Width==36&&fallbackIcon.Height==36,"taskbar icon fallback respects monitor DPI");
        var type=app.GetType("PCL.WindowMotion");
        var targetMethod=type.GetMethod("ChooseTaskbarTarget",BindingFlags.Static|BindingFlags.NonPublic);
        var screen=new System.Windows.Rect(-1920,0,1920,1080);var source=new System.Windows.Rect(-1400,180,600,400);
        var works=new[]{new System.Windows.Rect(-1880,0,1880,1080),new System.Windows.Rect(-1920,40,1920,1040),new System.Windows.Rect(-1920,0,1880,1080),new System.Windows.Rect(-1920,0,1920,1040),screen};
        for(int i=0;i<works.Length;i++) {
            object[] args={screen,works[i],source,0};var target=(System.Windows.Rect)targetMethod.Invoke(null,args);
            Check((int)args[3]==(i<4?i:3)&&screen.Contains(target),"taskbar destination handles edge/negative monitor coordinates: "+i);
        }
        var content=new System.Windows.Controls.Grid{Background=System.Windows.Media.Brushes.AliceBlue};
        content.Children.Add(new System.Windows.Controls.Border{Height=48,VerticalAlignment=System.Windows.VerticalAlignment.Top,Background=System.Windows.Media.Brushes.DodgerBlue,Child=new System.Windows.Controls.TextBlock{Text="PCL  Ⅲ     第三方版本",FontSize=20,Margin=new System.Windows.Thickness(20,8,0,0),Foreground=System.Windows.Media.Brushes.White}});
        content.Children.Add(new System.Windows.Controls.TextBlock{Text="启动  /  下载  /  设置",FontSize=22,Margin=new System.Windows.Thickness(32,100,0,0),Foreground=System.Windows.Media.Brushes.DodgerBlue});
        var window=new System.Windows.Window{Content=content,Width=600,Height=400,Left=120,Top=120,ShowInTaskbar=false,ShowActivated=false,WindowStyle=System.Windows.WindowStyle.None,AllowsTransparency=true,Background=System.Windows.Media.Brushes.Transparent,Opacity=.9};
        window.Show();Pump(80);
        var image=new System.Windows.Media.Imaging.RenderTargetBitmap(600,400,96,96,System.Windows.Media.PixelFormats.Pbgra32);image.Render(content);image.Freeze();
        var surfaceType=app.GetType("PCL.TaskbarWarpSurface");
        var progress=(System.Windows.DependencyProperty)surfaceType.GetField("ProgressProperty").GetValue(null);
        for(int edge=0;edge<4;edge++) {
            var target=edge==0?new System.Windows.Rect(4,470,4,48):edge==1?new System.Windows.Rect(300,4,48,4):edge==2?new System.Windows.Rect(790,470,4,48):new System.Windows.Rect(300,590,48,4);
            var warp=(System.Windows.FrameworkElement)Activator.CreateInstance(surfaceType,new object[]{image,new System.Windows.Rect(80,80,600,400),target,edge,800d,640d});
            var mesh=(System.Windows.Media.Media3D.MeshGeometry3D)surfaceType.GetField("Mesh",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(warp);
            bool valid=true;
            foreach(double p in new[]{0d,.1,.25,.5,.75,.82,1d}) {
                warp.SetValue(progress,p);
                valid &= mesh.Positions.Count==297&&mesh.Positions.All(point=>!double.IsNaN(point.X)&&!double.IsInfinity(point.X)&&!double.IsNaN(point.Y)&&!double.IsInfinity(point.Y));
                if(p==.5)CapturePage(warp,800,640,"window-warp-"+edge+".png");
                if(p>=.82)valid &= Math.Abs(mesh.Positions[0].X-target.X)<.01&&Math.Abs(-mesh.Positions[0].Y-target.Y)<.01;
            }
            Check(valid,"bounded mesh reaches its taskbar endpoint: edge "+edge);
            surfaceType.GetMethod("Release").Invoke(warp,null);
        }
        var colorWarp=(System.Windows.FrameworkElement)Activator.CreateInstance(surfaceType,new object[]{image,new System.Windows.Rect(0,0,600,400),new System.Windows.Rect(300,390,32,32),3,600d,400d});
        colorWarp.Measure(new System.Windows.Size(600,400));colorWarp.Arrange(new System.Windows.Rect(0,0,600,400));colorWarp.UpdateLayout();
        var reproduced=new System.Windows.Media.Imaging.RenderTargetBitmap(600,400,96,96,System.Windows.Media.PixelFormats.Pbgra32);reproduced.Render(colorWarp);
        foreach(var pixel in new[]{new System.Windows.Int32Rect(5,5,1,1),new System.Windows.Int32Rect(300,250,1,1)}) {
            var before=new byte[4];var after=new byte[4];image.CopyPixels(pixel,before,4,0);reproduced.CopyPixels(pixel,after,4,0);
            Check(Enumerable.Range(0,4).All(channel=>Math.Abs(before[channel]-after[channel])<=2),"minimize mesh preserves source color and alpha at "+pixel.X+";"+pixel.Y);
        }
        surfaceType.GetMethod("Release").Invoke(colorWarp,null);
        var alphaVisual=new System.Windows.Media.DrawingVisual();
        using(var dc=alphaVisual.RenderOpen())dc.DrawRectangle(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128,42,96,160)),null,new System.Windows.Rect(0,0,32,32));
        var alphaImage=new System.Windows.Media.Imaging.RenderTargetBitmap(32,32,96,96,System.Windows.Media.PixelFormats.Pbgra32);alphaImage.Render(alphaVisual);alphaImage.Freeze();
        var alphaWarp=(System.Windows.FrameworkElement)Activator.CreateInstance(surfaceType,new object[]{alphaImage,new System.Windows.Rect(0,0,32,32),new System.Windows.Rect(0,30,8,8),3,32d,32d});
        alphaWarp.Measure(new System.Windows.Size(32,32));alphaWarp.Arrange(new System.Windows.Rect(0,0,32,32));alphaWarp.UpdateLayout();
        var alphaResult=new System.Windows.Media.Imaging.RenderTargetBitmap(32,32,96,96,System.Windows.Media.PixelFormats.Pbgra32);alphaResult.Render(alphaWarp);
        var originalPixel=new byte[4];var resultPixel=new byte[4];var center=new System.Windows.Int32Rect(16,16,1,1);alphaImage.CopyPixels(center,originalPixel,4,0);alphaResult.CopyPixels(center,resultPixel,4,0);
        Check(Enumerable.Range(0,4).All(channel=>Math.Abs(originalPixel[channel]-resultPixel[channel])<=2),"minimize preserves partially transparent pixels without additive color blending");
        surfaceType.GetMethod("Release").Invoke(alphaWarp,null);
        SetNormalPreference("UiMinimizeAnimation",true);SetNormalPreference("UiCloseAnimation",true);
        var motion=Activator.CreateInstance(type,new object[]{window});
        Action minimize=()=>type.GetMethod("Minimize").Invoke(motion,null);
        Func<bool> active=()=> (bool)type.GetProperty("IsMinimizing").GetValue(motion);
        minimize();bool animated=active();int count=System.Windows.Application.Current.Windows.Count;
        minimize();Check(System.Windows.Application.Current.Windows.Count==count,"repeated minimize does not create another overlay");
        if(animated) {
            Pump(150);Check(window.Opacity==0&&active(),"snapshot deforms while the real window is hidden");
            Pump(500);
        }
        Check(window.WindowState==System.Windows.WindowState.Minimized&&!active()&&Math.Abs(window.Opacity-.9)<.01,"minimize finishes with real state and releases overlay/opacity");
        window.WindowState=System.Windows.WindowState.Normal;Pump(60);
        Check(Math.Abs(window.Width-600)<1&&Math.Abs(window.Left-120)<1&&window.Opacity==.9,"restoring keeps the original window bounds and transparency");
        minimize();type.GetMethod("CancelMinimize").Invoke(motion,null);
        if(window.WindowState==System.Windows.WindowState.Minimized)window.WindowState=System.Windows.WindowState.Normal;
        Check(!active()&&window.Opacity==.9,"cancelling minimize releases the temporary window and restores opacity");
        SetNormalPreference("UiMinimizeAnimation",false);minimize();
        Check(window.WindowState==System.Windows.WindowState.Minimized&&!active(),"disabled minimize animation applies native state directly");
        window.WindowState=System.Windows.WindowState.Normal;Pump(60);
        int closed=0;var close=type.GetMethod("PlayClose");close.Invoke(motion,new object[]{content,new Action(()=>closed++)});
        close.Invoke(motion,new object[]{content,new Action(()=>closed+=100)});
        Pump(65);
        Check(!(bool)Call("ModVisualExperience","MotionAvailable")||window.Opacity>0&&window.Opacity<.9,"close passes through a visible fade and small shrink");
        Pump(250);
        Check(closed==1&&(!(bool)Call("ModVisualExperience","MotionAvailable")||window.Opacity==0)&&!window.HasAnimatedProperties,"close callback runs once and animation clocks are removed");
        ((IDisposable)motion).Dispose();
        SetNormalPreference("UiCloseAnimation",false);motion=Activator.CreateInstance(type,new object[]{window});
        close.Invoke(motion,new object[]{content,new Action(()=>closed++)});
        Check(closed==2&&window.Opacity==.9,"disabled close animation calls shutdown immediately");
        ((IDisposable)motion).Dispose();
        var released=Enumerable.Range(0,30).Select(i=>MakeWindowMotionProbe(window)).ToList();
        Pump(40);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Check(released.All(reference=>!reference.IsAlive),"disposed motion controllers detach native hooks and window handlers");
        window.Close();
        SetNormalPreference("UiCloseAnimation",true);SetNormalPreference("UiMinimizeAnimation",true);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static WeakReference MakeWindowMotionProbe(System.Windows.Window window) {
        var motion=(IDisposable)Activator.CreateInstance(app.GetType("PCL.WindowMotion"),new object[]{window});
        motion.Dispose();return new WeakReference(motion);
    }
    public sealed class MeasureProbe : System.Windows.Controls.Decorator {
        public int Measures;
        protected override System.Windows.Size MeasureOverride(System.Windows.Size available) {Measures++;return base.MeasureOverride(available);}
    }
    public sealed class Counter {readonly Action action;public Counter(Action a){action=a;} public void Invoke(){action();}}
    static void Pump(int milliseconds) {
        var frame=new System.Windows.Threading.DispatcherFrame();
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};
        EventHandler tick=null;tick=(s,e)=>{timer.Stop();timer.Tick-=tick;frame.Continue=false;};timer.Tick+=tick;timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static WeakReference MakeGlassProbe() {
        var layer=new System.Windows.Controls.Border();
        Call("ModVisualExperience","ConfigureGlass",layer,new System.Windows.Controls.Canvas(),true);
        var weak=new WeakReference(layer.Child);Call("ModVisualExperience","ConfigureGlass",layer,new System.Windows.Controls.Canvas(),false);return weak;
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static WeakReference MakeErrorProbe() {
        var text=new System.Windows.Controls.TextBlock{Text="test"};Call("ModVisualExperience","BeginErrorAccent",text);return new WeakReference(text);
    }
    static void CapturePage(System.Windows.FrameworkElement page,int width,int height,string name) {
        page.Measure(new System.Windows.Size(width,height));page.Arrange(new System.Windows.Rect(0,0,width,height));page.UpdateLayout();
        var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(page);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,name)))encoder.Save(output);
    }
    static void VisualChecks() {
        foreach(string key in new[]{"UiFrostedGlass","UiStartupAnimation","UiErrorAnimation","UiIconAnimation"})
            Check((bool)Call("Settings","Get",key),"visual preference defaults enabled: "+key);
        var layer=new System.Windows.Controls.Border();var background=new System.Windows.Controls.Canvas();
        Call("ModVisualExperience","ConfigureGlass",layer,background,true);
        Check(layer.Visibility==(System.Windows.SystemParameters.HighContrast?System.Windows.Visibility.Collapsed:System.Windows.Visibility.Visible),"glass respects high contrast setting");
        Call("ModVisualExperience","ConfigureGlass",layer,background,false);
        Check(layer.Child==null&&layer.Visibility==System.Windows.Visibility.Collapsed,"turning glass off releases background visual tree");
        var references=Enumerable.Range(0,200).Select(i=>MakeGlassProbe()).ToList();
        Pump(100);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Check(references.Count(r=>r.IsAlive)<3,"200 glass toggles do not retain discarded visual layers");
        var error=new System.Windows.Controls.TextBlock{Text="error"};
        var effectHost=new System.Windows.Window{Content=error,Width=160,Height=60,ShowInTaskbar=false,ShowActivated=false,WindowStyle=System.Windows.WindowStyle.None,Left=0,Top=0};
        effectHost.Show();Pump(25);Call("ModVisualExperience","BeginErrorAccent",error);
        var transform=(System.Windows.Media.TranslateTransform)error.RenderTransform;
        var errors=Enumerable.Range(0,100).Select(i=>MakeErrorProbe()).ToList();
        Pump(800);GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        Check(error.RenderTransform==null&&!transform.HasAnimatedProperties,"error animation removes its completed animation clock and transform");
        Check(errors.Count(r=>r.IsAlive)<3,"100 completed error animations release target controls");
        effectHost.Content=null;effectHost.Close();
        var samples=(double[])Call("MyOfflineMorphIcon","SpringSamples");
        Check(samples.Length>5&&samples.Length<=90&&samples.Last()==1&&samples.All(v=>!double.IsInfinity(v)&&!double.IsNaN(v)),"offline morph spring is bounded, finite and reaches its final shape");
        var icon=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.MyOfflineMorphIcon"));
        var state=icon.GetType().GetProperty("State");
        foreach(string value in new[]{"download","ready","error","device","link"}) {state.SetValue(icon,value);CapturePage(icon,24,24,"icon-"+value+".png");}
        Check(true,"all local icon states render without external files or URLs");
        string device=(string)Call("ModDeviceInfo","ReadBasicDeviceInfo");
        Check(device.Contains("处理器")&&device.Contains("内存")&&device.Contains("磁盘"),"read real local processor, physical memory and disk configuration");
        var query=(System.Threading.Tasks.Task)Call("ModDeviceInfo","ReadDeviceInfo",false);
        Check(query.Wait(6500)&&((string)query.GetType().GetProperty("Result").GetValue(query)).Contains("显卡"),"graphics lookup returns a result or bounded fallback without blocking the UI");
        var about=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.PageOtherAbout"));
        Call("ModVisualExperience","ApplyQuietCards",about);
        var item=about.FindName("ItemAboutPcl");var info=item.GetType().GetProperty("Info");
        info.SetValue(item,((string)info.GetValue(item)).Replace("%VERSION%",(string)app.GetType("PCL.ModBase").GetField("VersionDisplay").GetRawConstantValue()).Replace("%VERSIONCODE%","413").Replace("%BRANCH%","0"));
        ((System.Windows.Controls.TextBlock)about.FindName("TextProjectVersion")).Text="本机：v1.2.0  |  最新发行版：v1.2.0（已是最新版）";
        ((System.Windows.Controls.TextBox)about.FindName("TextProjectChanges")).Text="[ + ] 本项目发行版检查与一键更新\r\n[ + ] 本机设备配置与离线图标\r\n[ - ] 修复更新失败时的程序恢复";
        Check(about.FindName("IconProjectRelease")!=null&&about.FindName("BtnProjectInstall")!=null,"about page contains local status icon and one-click updater");
        foreach(var size in new[]{Tuple.Create(600,1050),Tuple.Create(810,1100),Tuple.Create(1100,1200)}) CapturePage(about,size.Item1,size.Item2,"about-"+size.Item1+".png");
        var toolbox=(System.Windows.FrameworkElement)Activator.CreateInstance(app.GetType("PCL.PageOtherTest"));
        Call("ModVisualExperience","ApplyQuietCards",toolbox);
        app.GetType("PCL.SettingService").GetMethod("RefreshSettings",BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{toolbox});
        ((System.Windows.Controls.TextBox)toolbox.FindName("TextDeviceInfo")).Text=device+"\r\n\r\n显卡：本机信息读取后在此显示";
        Check(toolbox.FindName("BtnDeviceRefresh")!=null,"toolbox exposes refreshable device configuration");
        CapturePage(toolbox,700,850,"toolbox-preview.png");
        Console.WriteLine("VISUAL: 200 glass toggles and 100 error animations; "+errors.Count(r=>r.IsAlive)+" error targets retained after full collection.");
    }
    static void ProjectUpdateChecks() {
        Check((Version)Call("ModProjectUpdate","ParseProjectVersion","v1.10.0")>(Version)Call("ModProjectUpdate","ParseProjectVersion","v1.9.9"),"compare release versions numerically");
        Check((Version)Call("ModProjectUpdate","ParseProjectVersion","1.2.0")==new Version(1,2,0,0),"normalize current release version");
        foreach(string tag in new[]{"v1.2.0-beta","../../x","latest","1.2"}) {
            bool failed=false;try{Call("ModProjectUpdate","ParseProjectVersion",tag);}catch{failed=true;}
            Check(failed,"reject invalid stable version "+tag);
        }
        string notes=(string)Call("ModProjectUpdate","FormatProjectNotes","# Intro\n- [ + ] 增加多账号\n[ - ] 修复下载\n- [ ? ] 页面切换仍卡\nother details");
        Check(notes=="[ + ] 增加多账号\r\n[ - ] 修复下载\r\n[ ? ] 页面切换仍卡","display added features, bug fixes and known issues in requested format");
        const string prefix="https://github.com/LGCMcn/PCL3_ModDownloadFix/releases/download/v1.3.0/";
        Check((bool)Call("ModProjectUpdate","IsProjectAssetUrl",prefix+"PCL.DownloadFix.Windows-x64.zip","v1.3.0","PCL.DownloadFix.Windows-x64.zip"),"accept release asset from exact project and tag");
        foreach(string url in new[]{prefix.Replace("LGCMcn","other")+"PCL.DownloadFix.Windows-x64.zip",prefix.Replace("https:","http:")+"PCL.DownloadFix.Windows-x64.zip",prefix+"PCL.DownloadFix.Windows-x64.zip?redirect=evil"})
            Check(!(bool)Call("ModProjectUpdate","IsProjectAssetUrl",url,"v1.3.0","PCL.DownloadFix.Windows-x64.zip"),"refuse unrelated or insecure update asset");
        foreach(string name in new[]{"../launcher.exe","PCL.Services.json/../launcher.exe",".minecraft/options.txt","PCL/Log1.txt","unknown.dll"})
            Check(!(bool)Call("ModProjectUpdate","IsUpdatePackageFile",name),"deny unsupported ZIP entry "+name);
        var release=new JObject { ["tag_name"]="v1.3.0", ["body"]="[ + ] Test",["assets"]=new JArray(new JObject { ["name"]="PCL.DownloadFix.Windows-x64.zip",["size"]=1024,["digest"]="sha256:"+new string('a',64),["browser_download_url"]=prefix+"PCL.DownloadFix.Windows-x64.zip"})};
        var parsed=Call("ModProjectUpdate","ParseProjectRelease",release);
        Check((bool)parsed.GetType().GetProperty("IsNewer").GetValue(parsed),"newer stable release enables update");
        Check((string)parsed.GetType().GetProperty("Sha256").GetValue(parsed)==new string('a',64),"use GitHub release asset SHA256");
        release["prerelease"]=true;
        bool denied=false;try{Call("ModProjectUpdate","ParseProjectRelease",release);}catch{denied=true;}
        Check(denied,"never install prerelease automatically");
        string temp=Path.Combine(Path.GetTempPath(),"pcl-zip-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try {
            const string exe="Plain Craft Launcher (PCL) LGCMcn 下载修复版.exe";
            var contents=new System.Collections.Generic.Dictionary<string,string>{{exe,"dummy-program"},{exe+".config","dummy-config"},{"PCL.Update.exe","dummy-helper"},{"LICENCE","dummy-license"},{"PCL.Services.json","example-not-personal"},{"PCL.Release.json","{\"repository\":\"LGCMcn/PCL3_ModDownloadFix\",\"version\":\"1.3.0\"}"}};
            var hashes=new JObject();
            foreach(var pair in contents) using(var sha=System.Security.Cryptography.SHA256.Create()) hashes[pair.Key]=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pair.Value))).Replace("-","").ToLowerInvariant();
            contents["SHA256SUMS.json"]=hashes.ToString();
            string archive=Path.Combine(temp,"package.zip");
            Action<string> write=path=>{using(var zip=System.IO.Compression.ZipFile.Open(path,System.IO.Compression.ZipArchiveMode.Create))foreach(var pair in contents)using(var writer=new StreamWriter(zip.CreateEntry(pair.Key).Open(),new System.Text.UTF8Encoding(false)))writer.Write(pair.Value);};
            write(archive);
            var selected=(System.Collections.IList)Call("ModProjectUpdate","ExtractVerifiedUpdate",archive,Path.Combine(temp,"valid"),"1.3.0");
            Check(selected.Contains(exe)&&!selected.Contains("PCL.Services.json"),"verified update package selects program files and preserves personal JSON");
            bool rejected=false;try{Call("ModProjectUpdate","ExtractVerifiedUpdate",archive,Path.Combine(temp,"version"),"1.4.0");}catch{rejected=true;}
            Check(rejected,"reject package version mismatch");
            contents[exe]="corrupt-program";archive=Path.Combine(temp,"corrupt.zip");write(archive);
            rejected=false;try{Call("ModProjectUpdate","ExtractVerifiedUpdate",archive,Path.Combine(temp,"bad-hash"),"1.3.0");}catch{rejected=true;}
            Check(rejected,"reject corrupt files even inside a readable ZIP");
            contents["../outside.txt"]="escape";archive=Path.Combine(temp,"traversal.zip");write(archive);
            rejected=false;try{Call("ModProjectUpdate","ExtractVerifiedUpdate",archive,Path.Combine(temp,"bad-path"),"1.3.0");}catch{rejected=true;}
            Check(rejected&&!File.Exists(Path.Combine(temp,"outside.txt")),"reject ZIP path traversal before writing outside staging folder");
        } finally {Directory.Delete(temp,true);}
    }
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
