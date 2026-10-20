using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

internal static class UpdaterTests {
    static int passed;
    static void Check(bool value,string message) {if(!value) throw new Exception(message);Console.WriteLine("PASS: "+message);passed++;}
    static XElement Plan(string root,string stage,bool broken) {
        Directory.CreateDirectory(root);Directory.CreateDirectory(Path.Combine(stage,"payload"));
        File.WriteAllText(Path.Combine(root,"launcher.exe"),"old-program");
        File.WriteAllText(Path.Combine(root,"PCL.Services.json"),"personal-secret-placeholder");
        File.WriteAllText(Path.Combine(stage,"payload","launcher.exe"),"new-program");
        File.WriteAllText(Path.Combine(stage,"payload","PCL.Release.json"),"new-version");
        var plan=new XElement("update",new XAttribute("directory",root),new XAttribute("executable","launcher.exe"));
        foreach(string name in new[]{"launcher.exe","PCL.Release.json"})
            plan.Add(new XElement("file",new XAttribute("source",name),new XAttribute("target",name),new XAttribute("sha256",Updater.Hash(Path.Combine(stage,"payload",name)))));
        if(broken) {
            Directory.CreateDirectory(Path.Combine(root,"LICENCE"));
            File.WriteAllText(Path.Combine(stage,"payload","LICENCE"),"test");
            plan.Add(new XElement("file",new XAttribute("source","LICENCE"),new XAttribute("target","LICENCE"),new XAttribute("sha256",Updater.Hash(Path.Combine(stage,"payload","LICENCE")))));
        }
        return plan;
    }
    static void Deny(Action action,string message) {bool failed=false;try{action();}catch(IOException){failed=true;}Check(failed,message);}
    static int Main() {
        string temporary=Path.Combine(Path.GetTempPath(),"pcl-update-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try {
            foreach(string bad in new[]{"../outside","x/../../outside","C:/outside","x\\outside","x:stream","x./file","x /file","/outside"})
                Deny(()=>Updater.SafePath(temporary,bad),"reject unsafe update target "+bad);
            string root=Path.Combine(temporary,"success"),stage=Path.Combine(temporary,"stage-success");
            Updater.Apply(Plan(root,stage,false),stage,false);
            Check(File.ReadAllText(Path.Combine(root,"launcher.exe"))=="new-program","replace executable after complete backup");
            Check(File.ReadAllText(Path.Combine(root,"PCL.Services.json"))=="personal-secret-placeholder","preserve personal service configuration");
            Check(Directory.GetFiles(Path.Combine(root,".pcl-update-backups"),"launcher.exe",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p)=="old-program"),"retain previous executable in recovery backup");
            root=Path.Combine(temporary,"rollback");stage=Path.Combine(temporary,"stage-rollback");
            var plan=Plan(root,stage,true);
            Deny(()=>Updater.Apply(plan,stage,false),"replacement failure reports error");
            Check(File.ReadAllText(Path.Combine(root,"launcher.exe"))=="old-program","restore old executable after partial replacement");
            Check(!File.Exists(Path.Combine(root,"PCL.Release.json")),"remove newly added files on rollback");
            root=Path.Combine(temporary,"corruption");stage=Path.Combine(temporary,"stage-corruption");
            plan=Plan(root,stage,false);File.WriteAllText(Path.Combine(stage,"payload","launcher.exe"),"tampered");
            Deny(()=>Updater.Apply(plan,stage,false),"reject changed staging payload before replacing anything");
            Check(File.ReadAllText(Path.Combine(root,"launcher.exe"))=="old-program","corrupt payload leaves original executable untouched");
            plan=Plan(Path.Combine(temporary,"secret-target"),Path.Combine(temporary,"stage-secret"),false);
            plan.Elements("file").First().SetAttributeValue("target","PCL.Services.json");
            Deny(()=>Updater.Apply(plan,Path.Combine(temporary,"stage-secret"),false),"helper refuses a plan targeting personal configuration");
            Console.WriteLine("RESULT: "+passed+" updater checks passed");return 0;
        } catch(Exception error) {Console.Error.WriteLine(error);return 1;}
        finally {Directory.Delete(temporary,true);}
    }
}
