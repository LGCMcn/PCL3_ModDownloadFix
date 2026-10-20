// Independent update helper: waits for normal exit, backs up all targets, then replaces program files.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

internal static class Updater {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool MoveFileEx(string source,string target,int flags);
    internal static string Hash(string path) {
        using(var input=File.OpenRead(path)) using(var sha=SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();
    }
    internal static string SafePath(string root,string name) {
        if(string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(":") || name.Contains("\\") ||
           name.Split('/').Any(p=>p=="" || p=="." || p==".." || p.EndsWith(".") || p.EndsWith(" ")))
            throw new IOException("更新路径无效。");
        string full=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar)));
        if(!full.StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)) throw new IOException("更新路径越界。");
        for(string p=full; p!=null; p=Path.GetDirectoryName(p)) {
            if((File.Exists(p)||Directory.Exists(p)) && (File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0) throw new IOException("更新路径包含链接。");
        }
        return full;
    }
    static void Replace(string source,string target) {
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        string next=target+".update-"+Guid.NewGuid().ToString("N");
        try {
            File.Copy(source,next,false);
            if(!MoveFileEx(next,target,3)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        } finally {if(File.Exists(next)) File.Delete(next);}
    }
    internal static void Apply(XElement plan,string stage,bool waitForParent) {
        string directory=Path.GetFullPath((string)plan.Attribute("directory"));
        string executable=(string)plan.Attribute("executable");
        if(Path.GetFileName(executable)!=executable || !executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) throw new IOException("启动文件无效。");
        string exe=SafePath(directory,executable), payload=Path.Combine(stage,"payload");
        var files=plan.Elements("file").ToList();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] allowed={executable,executable+".config","PCL.Update.exe","LICENCE","第三方版本声明.md","使用说明.md","项目分析与验证.md","PCL.Release.json","SHA256SUMS.json","组件/easytier-windows-x86_64-v2.6.4.zip"};
        foreach(var file in files) {
            string name=(string)file.Attribute("target"),source=(string)file.Attribute("source");
            if(!allowed.Contains(name,StringComparer.Ordinal) || !seen.Add(name)) throw new IOException("更新目标不受支持或重复。");
            SafePath(directory,name);
            if(Hash(SafePath(payload,source))!=(string)file.Attribute("sha256")) throw new IOException("更新暂存文件校验失败。");
        }
        if(!seen.Contains(executable) || !seen.Contains("PCL.Release.json")) throw new IOException("更新计划缺少程序或版本文件。");
        if(waitForParent) {
            try {
                using(var parent=Process.GetProcessById((int)plan.Attribute("pid"))) {
                    if(parent.StartTime.ToUniversalTime().Ticks==(long)plan.Attribute("start") && !parent.WaitForExit(120000))
                        throw new IOException("启动器尚未退出，更新取消。请先关闭启动器再重试。");
                }
            } catch(ArgumentException) { /* Parent already exited. */ }
        }
        string backup=SafePath(directory,".pcl-update-backups/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(backup);
        var originals=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        var changed=new List<string>();
        try {
            // Snapshot every target before modifying the first one; existing personal JSON is never in this list.
            foreach(var file in files) {
                string name=(string)file.Attribute("target"), target=SafePath(directory,name);
                bool exists=File.Exists(target); originals[name]=exists;
                if(exists) {string saved=SafePath(backup,name);Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Copy(target,saved,false);}
            }
            File.WriteAllText(Path.Combine(backup,"update-status.txt"),"Replacing program files. Personal configuration and game folders are preserved.");
            foreach(var file in files) {
                string name=(string)file.Attribute("target"),target=SafePath(directory,name);
                changed.Add(name);
                Replace(SafePath(payload,(string)file.Attribute("source")),target);
                if(Hash(target)!=(string)file.Attribute("sha256")) throw new IOException("替换后的文件校验失败。");
            }
            File.WriteAllText(Path.Combine(backup,"update-status.txt"),"Update completed. Previous files are available here for recovery.");
        } catch(Exception failure) {
            var failures=new List<string>();
            foreach(string name in changed.AsEnumerable().Reverse()) {
                try {
                    string target=SafePath(directory,name);
                    if(originals[name]) Replace(SafePath(backup,name),target); else if(File.Exists(target)) File.Delete(target);
                } catch(Exception rollback) {failures.Add(name+": "+rollback.Message);}
            }
            string message="更新失败。"+(failures.Count==0?"已恢复原程序。":"部分文件恢复失败，请使用备份手动恢复："+backup)+"\n"+failure.Message+"\n"+string.Join("\n",failures);
            File.WriteAllText(Path.Combine(backup,"update-status.txt"),message);
            throw new IOException(message,failure);
        }
    }
    [STAThread] static int Main(string[] args) {
        string stage=null,exe=null;
        try {
            if(args.Length!=1) throw new ArgumentException("请在启动器的关于页面使用更新按钮。");
            string planPath=Path.GetFullPath(args[0]);stage=Path.GetDirectoryName(planPath);
            if(!string.Equals(stage,AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) throw new IOException("更新计划必须与更新工具位于同一暂存目录。");
            XElement plan;
            using(var reader=XmlReader.Create(planPath,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024})) plan=XElement.Load(reader);
            exe=SafePath((string)plan.Attribute("directory"),(string)plan.Attribute("executable"));
            Apply(plan,stage,true);
            Process.Start(new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)});
            try {Directory.Delete(Path.Combine(stage,"payload"),true);File.Delete(Path.Combine(stage,"package.zip"));} catch(IOException) {}
            return 0;
        } catch(Exception error) {
            if(stage!=null) try{File.WriteAllText(Path.Combine(stage,"update-error.txt"),error.ToString());}catch(IOException){}
            MessageBox.Show(error.Message+"\n更新日志："+stage,"第三方版本更新",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            return 1;
        }
    }
}
