using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Automation;
using System.Web.Script.Serialization;
public static class NavigationCapture {
 [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr c);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 static object Capture(IntPtr hwnd,int[] steps,string output) {
  var root=AutomationElement.FromHandle(hwnd);var buttons=new Dictionary<int,AutomationElement>();
  foreach(int i in steps.Distinct())buttons[i]=root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,"BtnTitleSelect"+i));
  if(buttons.Values.Any(b=>b==null))throw new Exception("Navigation automation element missing.");
  var bounds=buttons.Values.Select(b=>b.Current.BoundingRectangle).Where(r=>r.Width>0).ToArray();
  int left=(int)Math.Floor(bounds.Min(r=>r.Left))-8,top=(int)Math.Floor(bounds.Min(r=>r.Top)),width=(int)Math.Ceiling(bounds.Max(r=>r.Right))-left+8,height=(int)Math.Ceiling(bounds.Max(r=>r.Height));
  var cases=new List<object>();
  using(var bmp=new Bitmap(width,height,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bmp)){
   foreach(int step in steps){
    if(GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Keep the launcher in the foreground while profiling.");
    var button=buttons[step];var target=button.Current.BoundingRectangle;var samples=new List<object>();var positions=new List<Tuple<double,double>>();
    var watch=Stopwatch.StartNew();((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();double invokeMs=watch.Elapsed.TotalMilliseconds;
    int n=0;
    while(watch.ElapsedMilliseconds<650){
     if(GetForegroundWindow()!=hwnd)throw new InvalidOperationException("The launcher lost foreground focus; capture stopped.");
     g.CopyFromScreen(left,top,0,0,bmp.Size,CopyPixelOperation.SourceCopy);
     var data=bmp.LockBits(new Rectangle(0,Math.Min(4,height-1),width,1),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
     var row=new byte[width*4];Marshal.Copy(data.Scan0,row,0,row.Length);bmp.UnlockBits(data);
     int bestStart=-1,bestCount=0,start=-1;
     for(int x=0;x<=width;x++){
      bool white=x<width&&row[x*4]>242&&row[x*4+1]>242&&row[x*4+2]>242;
      if(white&&start<0)start=x;
      if(!white&&start>=0){if(x-start>bestCount){bestStart=start;bestCount=x-start;}start=-1;}
     }
     double ms=watch.Elapsed.TotalMilliseconds;double center=bestCount>=16&&bestCount<=180?left+bestStart+bestCount/2d:double.NaN;
     if(!double.IsNaN(center))positions.Add(Tuple.Create(ms,center));
     samples.Add(new{ms=Math.Round(ms,2),x=double.IsNaN(center)?(double?)null:center,white_run=bestCount});
     if(n==0||n==5||n==12)bmp.Save(Path.Combine(output,"step-"+cases.Count+"-"+n+".png"));n++;Thread.Sleep(5);
    }
    double goal=target.X+target.Width/2,first=-1,pause=0,pauseStart=0,lastX=double.NaN;
    foreach(var p in positions){if(first<0&&Math.Abs(p.Item2-positions[0].Item2)>1.5)first=p.Item1;if(Math.Abs(p.Item2-goal)>3){if(Math.Abs(p.Item2-lastX)>.75)pauseStart=p.Item1;else pause=Math.Max(pause,p.Item1-pauseStart);}lastX=p.Item2;}
    var record=new{target=step,invoke_ms=Math.Round(invokeMs,2),first_change_ms=Math.Round(first,2),max_stationary_ms=Math.Round(pause,2),distinct_positions=positions.Select(p=>p.Item2).Distinct().Count(),valid_samples=positions.Count,goal_x=goal,samples};cases.Add(record);
    Console.WriteLine("target="+step+" invoke_ms="+Math.Round(invokeMs,1)+" first_change_ms="+Math.Round(first,1)+" stationary_ms="+Math.Round(pause,1)+" positions="+positions.Select(p=>p.Item2).Distinct().Count()+" foreground="+(GetForegroundWindow()==hwnd));Thread.Sleep(200);
   }
  }
  return cases;
 }
 public static int Main(string[] args){try{SetProcessDpiAwarenessContext(new IntPtr(-4));Directory.CreateDirectory(args[1]);var result=Capture(new IntPtr(long.Parse(args[0])),new[]{1,3,4,0,3,1,0},args[1]);File.WriteAllText(Path.Combine(args[1],"frames.json"),new JavaScriptSerializer().Serialize(result));return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}
