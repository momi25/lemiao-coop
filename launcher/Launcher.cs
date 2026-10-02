// Authored launcher. No game assets or credentials are embedded.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

public class Release
{
    public int schema {get;set;} public long generation {get;set;}
    public string version {get;set;} public string installer_url {get;set;}
    public string installer_sha256 {get;set;} public string notes {get;set;}
}
public class Installation
{
    public long generation {get;set;} public string version {get;set;}
    public string folder {get;set;} public string build {get;set;}
}
public class State
{
    public string game {get;set;} public Installation active {get;set;}
    public Installation previous {get;set;} public bool updates_paused {get;set;}
}
public static class Core
{
    public static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=1024*1024};
    public static string Hash(string path) {using(var sha=SHA256.Create()) using(var file=File.OpenRead(path))
        return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
    public static string Quote(string s) {if(s.Contains("\""))throw new Exception("Invalid path.");
        return "\""+Regex.Replace(s,@"(\\+)$","$1$1")+"\"";}
    public static bool GameRunning(){return Process.GetProcessesByName("eldenring").Length!=0;}
    public static bool LaunchAllowed(bool clicked,bool ready,bool working,bool running){return clicked&&ready&&!working&&!running;}
    public static bool Within(string root,string path){return Path.GetFullPath(path).StartsWith(
        Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    public static Release Verified(byte[] data,byte[] signature){
        if(data.Length>65536 || signature.Length!=256)throw new Exception("Invalid update signature.");
        using(var rsa=new RSACryptoServiceProvider()){rsa.FromXmlString(Build.PublicKey);
            if(!rsa.VerifyData(data,"SHA256",signature))throw new Exception("The update signature did not match. Your installation was kept.");}
        var r=Json.Deserialize<Release>(Encoding.UTF8.GetString(data));Validate(r);return r;
    }
    public static void Validate(Release r){
        Uri uri;if(r==null||r.schema!=1||r.generation<1||String.IsNullOrWhiteSpace(r.version)||r.version.Length>100||
            !Regex.IsMatch(r.installer_sha256??"","^[a-f0-9]{64}$")||!Uri.TryCreate(r.installer_url,UriKind.Absolute,out uri)||
            uri.Scheme!="https"||uri.Host!="github.com"||!uri.AbsolutePath.StartsWith("/momi25/lemiao-coop/releases/download/",StringComparison.Ordinal))
            throw new Exception("Unsupported update manifest. Your installation was kept.");
    }
    public static void AtomicState(string path,State s){
        Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".new";
        File.WriteAllText(temp,Json.Serialize(s),new UTF8Encoding(false));
        if(File.Exists(path))File.Replace(temp,path,path+".backup");else File.Move(temp,path);
    }
    public static void ValidateInstallation(Installation i){
        if(i==null||!Directory.Exists(i.folder)||!Within(i.folder,i.build)||
            !File.Exists(Path.Combine(i.build,"Launch-Maliketh-Coop.ps1")))throw new Exception("Installation is incomplete. The previous build was kept.");
        var release=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(i.folder,"RELEASE.json")));
        var files=(Dictionary<string,object>)release["gameplay_files"];
        foreach(var entry in files){string p=Path.GetFullPath(Path.Combine(i.build,"mod",entry.Key.Replace('/',Path.DirectorySeparatorChar)));
            if(!Within(Path.Combine(i.build,"mod"),p)||!File.Exists(p)||Hash(p)!=(string)entry.Value)
                throw new Exception("A gameplay file did not match: "+entry.Key+". The previous build was kept.");}
        if(Hash(Path.Combine(i.build,"remnant_maliketh.dll"))!=(string)release["controller_sha256"])
            throw new Exception("The controller checksum did not match.");
    }
    public static string FindGame(){
        var roots=new List<string>();string steam=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) as string;
        if(!String.IsNullOrEmpty(steam))roots.Add(steam);roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
        foreach(string root in roots.ToArray()){string vdf=Path.Combine(root,"steamapps","libraryfolders.vdf");
            if(File.Exists(vdf))foreach(Match m in Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s*\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}
        return roots.Select(root=>Path.Combine(root,"steamapps","common","ELDEN RING","Game")).FirstOrDefault(p=>File.Exists(Path.Combine(p,"eldenring.exe")));
    }
    public static byte[] Download(string url,int limit){
        var req=(HttpWebRequest)WebRequest.Create(url);req.UserAgent="LemiaoLauncher/1.0";req.Timeout=20000;req.ReadWriteTimeout=20000;
        req.MaximumAutomaticRedirections=8;req.Headers[HttpRequestHeader.CacheControl]="no-cache";
        using(var res=(HttpWebResponse)req.GetResponse()){
            if(res.ResponseUri.Scheme!="https"||res.ContentLength>limit)throw new Exception("Unexpected update download.");
            using(var input=res.GetResponseStream())using(var output=new MemoryStream()){
                var buffer=new byte[65536];int n;while((n=input.Read(buffer,0,buffer.Length))!=0){
                    if(output.Length+n>limit)throw new Exception("Update download is too large.");output.Write(buffer,0,n);}return output.ToArray();}}
    }
}
// The installer and its children belong to this private job. Closing the
// launcher cancels its build helpers; an Elden Ring process is never attached.
public sealed class BuildJob:IDisposable
{
    IntPtr handle;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr job,int kind,IntPtr data,uint length);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    public BuildJob(){handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)throw new Exception("Could not prepare the update worker.");
        IntPtr data=Marshal.AllocHGlobal(144);try {for(int i=0;i<144;i++)Marshal.WriteByte(data,i,0);Marshal.WriteInt32(data,16,0x2000);
            if(!SetInformationJobObject(handle,9,data,144))throw new Exception("Could not prepare cancellable update workers.");}
        catch{Dispose();throw;}finally{Marshal.FreeHGlobal(data);}}
    public void Attach(Process p){if(!AssignProcessToJobObject(handle,p.Handle)){try{p.Kill();}catch{}throw new Exception("Could not attach the update worker.");}}
    public void Dispose(){IntPtr old=Interlocked.Exchange(ref handle,IntPtr.Zero);if(old!=IntPtr.Zero)CloseHandle(old);}
}
public class Crest:Control
{
    public Crest(){SetStyle(ControlStyles.SupportsTransparentBackColor,true);DoubleBuffered=true;BackColor=Color.Transparent;}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        float x=Width/2f,y=Height/2f;using(var p=new Pen(Color.FromArgb(178,144,87),1.4f)){
            g.DrawEllipse(p,x-61,y-61,122,122);g.DrawEllipse(p,x-51,y-51,102,102);
            for(int k=0;k<12;k++){double a=k*Math.PI/6;g.DrawLine(p,x+(float)Math.Cos(a)*68,y+(float)Math.Sin(a)*68,x+(float)Math.Cos(a)*73,y+(float)Math.Sin(a)*73);}
            var sword=new[]{new PointF(x,y-46),new PointF(x+7,y-29),new PointF(x+3,y+12),new PointF(x+25,y+18),new PointF(x+3,y+21),new PointF(x+3,y+37),new PointF(x,y+44),new PointF(x-3,y+37),new PointF(x-3,y+21),new PointF(x-25,y+18),new PointF(x-3,y+12),new PointF(x-7,y-29)};
            using(var brush=new SolidBrush(Color.FromArgb(202,167,104)))g.FillPolygon(brush,sword);}}
}
public class Launcher:Form
{
    readonly string home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LemiaoCoop","Launcher");
    State state;bool busy,offline,pendingBundled;volatile bool closing;Release pending;Process installer;BuildJob job;
    static readonly object logLock=new object();
    Button play,updates,settings,rollback;Label headline,detail,version;ProgressBar progress;TextBox notes;System.Windows.Forms.Timer timer;
    string statePath {get{return Path.Combine(home,"state.json");}}
    static Color Ink=Color.FromArgb(15,20,21),Card=Color.FromArgb(24,31,31),Gold=Color.FromArgb(211,175,109),Muted=Color.FromArgb(160,170,164);
    public Launcher(bool preview=false){
        Text="Lemiao Co-op";Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);ClientSize=new Size(900,610);MinimumSize=new Size(840,640);StartPosition=FormStartPosition.CenterScreen;
        BackColor=Ink;ForeColor=Color.FromArgb(237,235,225);Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        var title=new Label {Text="L E M I A O",Location=new Point(42,30),Size=new Size(470,48),Font=new Font("Georgia",29,FontStyle.Bold),ForeColor=Gold};Controls.Add(title);
        Controls.Add(new Label {Text="CO-OP  /  YOUR SHARED ADVENTURE",Location=new Point(44,86),Size=new Size(620,25),ForeColor=Muted,Font=new Font("Segoe UI",10)});
        version=new Label {Location=new Point(630,48),Size=new Size(230,43),TextAlign=ContentAlignment.MiddleRight,ForeColor=Gold};Controls.Add(version);
        var panel=new Panel {Location=new Point(40,139),Size=new Size(820,300),BackColor=Card,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};Controls.Add(panel);
        panel.Controls.Add(new Crest {Location=new Point(39,32),Size=new Size(160,160)});
        headline=new Label {Location=new Point(222,30),Size=new Size(560,37),Font=new Font("Segoe UI",19,FontStyle.Bold)};panel.Controls.Add(headline);
        detail=new Label {Location=new Point(224,82),Size=new Size(555,60),ForeColor=Muted};panel.Controls.Add(detail);
        play=Button("PLAY  SEAMLESS CO-OP",new Point(224,168),new Size(554,61),true);play.Click+=async(s,e)=>await Play();panel.Controls.Add(play);
        panel.Controls.Add(new Label {Text="The game starts only when you press Play.",Location=new Point(224,245),Size=new Size(555,26),ForeColor=Muted});
        updates=Button("CHECK UPDATES",new Point(40,458),new Size(195,44),false);updates.Click+=async(s,e)=>{state.updates_paused=false;Save();await Check();};Controls.Add(updates);
        settings=Button("CO-OP SETTINGS",new Point(247,458),new Size(195,44),false);settings.Click+=(s,e)=>{
            if(state.active!=null)Process.Start(new ProcessStartInfo("notepad.exe",Core.Quote(Path.Combine(state.active.build,"SeamlessCoop","ersc_settings.ini"))){UseShellExecute=false});};Controls.Add(settings);
        rollback=Button("PREVIOUS BUILD",new Point(454,458),new Size(195,44),false);rollback.Click+=(s,e)=>Rollback();Controls.Add(rollback);
        var logs=Button("VIEW LOG",new Point(661,458),new Size(199,44),false);logs.Click+=(s,e)=>{
            string log=Path.Combine(home,"launcher.log");if(File.Exists(log))Process.Start(new ProcessStartInfo("notepad.exe",Core.Quote(log)){UseShellExecute=false});};Controls.Add(logs);
        progress=new ProgressBar {Location=new Point(40,525),Size=new Size(820,5),Style=ProgressBarStyle.Marquee,Visible=false};Controls.Add(progress);
        notes=new TextBox {Location=new Point(40,545),Size=new Size(820,47),ReadOnly=true,Multiline=true,BorderStyle=BorderStyle.None,BackColor=Ink,ForeColor=Muted,Text="Updates are checked automatically. Your previous build stays available."};Controls.Add(notes);
        Directory.CreateDirectory(home);state=File.Exists(statePath)?Core.Json.Deserialize<State>(File.ReadAllText(statePath)):new State();
        if(state==null)state=new State();RefreshButtons();
        Shown+=async(s,e)=>{if(preview)return;try {if(String.IsNullOrEmpty(state.game)){state.game=Core.FindGame();
            if(state.game==null){using(var choose=new OpenFileDialog {Title="Select Elden Ring",Filter="Elden Ring|eldenring.exe"}){
                if(choose.ShowDialog(this)!=DialogResult.OK){Message("Select your game when you're ready.");return;}state.game=Path.GetDirectoryName(choose.FileName);}}
            Save();}await Check();}catch(Exception ex){Error(ex);}};
        timer=new System.Windows.Forms.Timer {Interval=2500};timer.Tick+=async(s,e)=>{
            RefreshButtons();if(pending!=null&&!busy&&!Core.GameRunning()&&!state.updates_paused)await Apply(pending,pendingBundled);};if(!preview)timer.Start();
        FormClosing+=(s,e)=>{closing=true;timer.Stop();if(busy){state.updates_paused=true;Save();if(job!=null)job.Dispose();}};
    }
    Button Button(string text,Point location,Size size,bool primary){var b=new Button {Text=text,Location=location,Size=size,FlatStyle=FlatStyle.Flat,
        BackColor=primary?Gold:Card,ForeColor=primary?Ink:Gold,Font=new Font("Segoe UI",primary?13:9,FontStyle.Bold),Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(64,75,67);b.FlatAppearance.BorderSize=primary?0:1;return b;}
    void Save(){Core.AtomicState(statePath,state);}
    void Log(string text){lock(logLock)File.AppendAllText(Path.Combine(home,"launcher.log"),DateTime.UtcNow.ToString("u")+" "+text+Environment.NewLine);}
    void Message(string text){if(!closing)notes.Text=text;Log(text);}
    void Error(Exception ex){Log(ex.ToString());if(closing)return;headline.Text="Update needs attention";detail.Text=ex.Message;Message("Your current installation was kept. View Log has the details.");}
    void RefreshButtons(){if(closing)return;bool running=Core.GameRunning();bool ready=state.active!=null;
        play.Enabled=Core.LaunchAllowed(true,ready,busy,running)&&(pending==null||state.updates_paused);updates.Enabled=!busy;
        settings.Enabled=ready&&!busy;rollback.Enabled=state.previous!=null&&!busy&&!running;progress.Visible=busy;
        version.Text=ready?state.active.version:"FIRST INSTALL";
        if(!busy){headline.Text=running?"Your adventure is running":ready?"Ready for your adventure":"Preparing your adventure";
            detail.Text=running?"Updates will wait until you quit through the game menu.":state.updates_paused?"Previous build selected. Press Check Updates to resume updates.":
                offline?"Offline: your installed build is available.":ready?"Maliketh. Loretta. Hoarah Loux. One launcher for your party.":"The first installation builds the mods from your own game files.";}}
    async Task Check(){if(busy)return;
        if(String.IsNullOrEmpty(state.game)){state.game=Core.FindGame();if(state.game==null){using(var choose=new OpenFileDialog {Title="Select Elden Ring",Filter="Elden Ring|eldenring.exe"}){
            if(choose.ShowDialog(this)!=DialogResult.OK)return;state.game=Path.GetDirectoryName(choose.FileName);}}Save();}
        busy=true;RefreshButtons();headline.Text="Checking for updates";detail.Text="Looking for the latest shared mod release.";
        Release latest=null;offline=false;
        try {latest=await Task.Run(()=>{byte[] data=Core.Download(Build.Feed,65536);
            byte[] sig=Convert.FromBase64String(Encoding.ASCII.GetString(Core.Download(Build.Feed+".sig",4096)).Trim());return Core.Verified(data,sig);});}
        catch(WebException){offline=true;Message("Couldn't reach the update server. Your installed build is still available.");}
        catch(Exception ex){Error(ex);busy=false;RefreshButtons();return;}
        busy=false;
        if(latest==null && state.active==null)latest=Build.BundledRelease();
        if(latest!=null&&(state.active==null||latest.generation>state.active.generation)){
            pending=latest;pendingBundled=offline;notes.Text=latest.notes;
            if(state.updates_paused){Message("Automatic updates are paused for the previous build.");}
            else if(Core.GameRunning()){Message("Update ready. Quit the game through its menu to install it.");}
            else await Apply(latest,offline);
        }else if(latest!=null)Message("You have the latest release. Both players should see the same version.");
        RefreshButtons();
    }
    async Task Apply(Release r,bool bundled){
        if(busy||Core.GameRunning())return;busy=true;RefreshButtons();headline.Text="Updating your adventure";detail.Text="Preparing a fresh installation. This may take several minutes.";
        try {
            string downloads=Path.Combine(home,"downloads");Directory.CreateDirectory(downloads);
            string exe=Path.Combine(downloads,"installer-"+r.generation+".exe");
            await Task.Run(()=>{
                if(!File.Exists(exe)||Core.Hash(exe)!=r.installer_sha256){
                    byte[] data;if(bundled){using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("installer.exe"))using(var memory=new MemoryStream()){resource.CopyTo(memory);data=memory.ToArray();}}
                    else data=Core.Download(r.installer_url,100*1024*1024);
                    string tmp=exe+".part";File.WriteAllBytes(tmp,data);
                    if(Core.Hash(tmp)!=r.installer_sha256){File.Delete(tmp);throw new Exception("Update checksum mismatch. The download was not run.");}
                    if(File.Exists(exe))File.Delete(exe);File.Move(tmp,exe);
                }});
            if(closing)throw new OperationCanceledException();
            if(Core.GameRunning())throw new Exception("The game started during download. Quit it through its menu to apply the update.");
            string destination=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LemiaoCoop","builds",r.generation+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            await Task.Run(()=>{
                var start=new ProcessStartInfo(exe,"--managed-install "+Core.Quote(state.game)+" "+Core.Quote(destination)){
                    UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(job=new BuildJob())using(installer=new Process {StartInfo=start}){
                    installer.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};installer.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Log(e.Data);};
                    if(closing)throw new OperationCanceledException();installer.Start();job.Attach(installer);installer.BeginOutputReadLine();installer.BeginErrorReadLine();installer.WaitForExit();
                    if(installer.ExitCode!=0)throw new Exception("The update build stopped. Check View Log; your previous build is still selected.");}
                installer=null;job=null;
            });
            var installed=Core.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(destination,"LAST-INSTALL.json")));
            var candidate=new Installation {generation=r.generation,version=r.version,folder=destination,build=(string)installed["build"]};
            await Task.Run(()=>Core.ValidateInstallation(candidate));
            if(closing)throw new OperationCanceledException();
            if(Core.GameRunning())throw new Exception("Quit the game through its menu before selecting the new build.");
            if(state.active!=null){string oldSettings=Path.Combine(state.active.build,"SeamlessCoop","ersc_settings.ini");
                string newSettings=Path.Combine(candidate.build,"SeamlessCoop","ersc_settings.ini");
                if(File.Exists(oldSettings))File.Copy(oldSettings,newSettings,true);}
            var next=new State {game=state.game,active=candidate,previous=state.active};Core.AtomicState(statePath,next);state=next;pending=null;
            Message("Update complete. Your settings were kept. Press Play when you want to start.");
            Shortcut();
        }catch(Exception ex){state.updates_paused=true;Save();Error(ex);}finally{busy=false;RefreshButtons();}
    }
    async Task Play(){
        // The only game-launching path. Startup, updates, tests and rollback never call this.
        if(!Core.LaunchAllowed(true,state.active!=null,busy,Core.GameRunning())||(pending!=null&&!state.updates_paused))return;
        if(!state.updates_paused){long previous=state.active.generation;await Check();
            if(closing||pending!=null||state.active==null||state.active.generation!=previous){Message("Update selected. Press Play when you're ready.");return;}}
        busy=true;RefreshButtons();
        try {await Task.Run(()=>Core.ValidateInstallation(state.active));
            if(closing||Core.GameRunning())return;
            string script=Path.Combine(state.active.build,"Launch-Maliketh-Coop.ps1");
            Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File "+Core.Quote(script)){
                UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=state.active.build});Message("Starting your modded Seamless session.");
        }catch(Exception ex){Error(ex);}finally{busy=false;RefreshButtons();}
    }
    void Rollback(){if(state.previous==null||busy||Core.GameRunning())return;
        try {Core.ValidateInstallation(state.previous);var next=new State {game=state.game,active=state.previous,previous=state.active,updates_paused=true};Core.AtomicState(statePath,next);state=next;pending=null;
            Message("Previous build selected; updates paused. Your saves were not restored or changed.");RefreshButtons();}catch(Exception ex){Error(ex);}}
    void Shortcut(){
        string stable=Path.Combine(home,"Lemiao-Launcher.exe");string current=Assembly.GetExecutingAssembly().Location;
        if(!String.Equals(stable,current,StringComparison.OrdinalIgnoreCase))File.Copy(current,stable,true);
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        dynamic shortcut=shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Lemiao Co-op.lnk"));
        shortcut.TargetPath=stable;shortcut.WorkingDirectory=home;shortcut.Description="Lemiao Co-op - updates and play";shortcut.Save();
    }
    public void Render(string path){ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-10000,-10000);Show();Application.DoEvents();
        headline.Text="Ready for your adventure";detail.Text="Maliketh. Loretta. Hoarah Loux. One launcher for your party.";version.Text="0.9.1  /  PET REPAIR";play.Enabled=true;
        using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(Point.Empty,Size));bitmap.Save(path);}Hide();}
}
static class Program
{
    [STAThread]static int Main(string[] args){
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        try {
            if(args.Length==2&&args[0]=="--self-test"){Tests.Run(args[1]);return 0;}
            if(args.Length==2&&args[0]=="--verify-feed"){var r=Core.Verified(Core.Download(Build.Feed,65536),Convert.FromBase64String(Encoding.ASCII.GetString(Core.Download(Build.Feed+".sig",4096)).Trim()));
                File.WriteAllText(args[1],Core.Json.Serialize(r));return 0;}
            if(args.Length==5&&args[0]=="--adopt"){
                string game=Core.FindGame();var install=new Installation {folder=Path.GetFullPath(args[1]),build=Path.GetFullPath(args[2]),generation=Int64.Parse(args[3]),version=args[4]};
                Core.ValidateInstallation(install);string home=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LemiaoCoop","Launcher");
                Core.AtomicState(Path.Combine(home,"state.json"),new State {game=game,active=install});return 0;}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==2&&args[0]=="--render-preview"){using(var form=new Launcher(true))form.Render(args[1]);return 0;}
            bool first;using(var single=new Mutex(true,@"Local\LemiaoCoopLauncher",out first)){
                if(!first)return 0;Application.Run(new Launcher());}return 0;
        }catch(Exception ex){
            if(args.Length>=2){File.WriteAllText(args[1]+".error.txt",ex.ToString());return 1;}
            MessageBox.Show(ex.Message,"Lemiao launcher");return 1;}
    }
}
static class Tests
{
    static int count;static void Require(bool condition){if(!condition)throw new Exception("Launcher regression failed at check "+count);count++;}
    public static void Run(string report){
        // Independent signed fixture generated by the private publisher.
        string dir=Path.GetDirectoryName(Path.GetFullPath(report));byte[] data=File.ReadAllBytes(Path.Combine(dir,"fixture.json"));
        byte[] sig=Convert.FromBase64String(File.ReadAllText(Path.Combine(dir,"fixture.sig")));var release=Core.Verified(data,sig);Require(release.generation==1);
        byte[] modified=(byte[])data.Clone();modified[0]^=1;bool rejected=false;try{Core.Verified(modified,sig);}catch{rejected=true;}Require(rejected);
        rejected=false;sig[0]^=1;try{Core.Verified(data,sig);}catch{rejected=true;}Require(rejected);
        foreach(bool ready in new[]{false,true})foreach(bool busy in new[]{false,true})foreach(bool running in new[]{false,true})
            Require(!Core.LaunchAllowed(false,ready,busy,running));
        Require(Core.LaunchAllowed(true,true,false,false));Require(!Core.LaunchAllowed(true,true,true,false));Require(!Core.LaunchAllowed(true,true,false,true));
        Require(Core.Within(dir,Path.Combine(dir,"child","file")));Require(!Core.Within(dir,Path.Combine(dir,"..","escape")));
        string statePath=Path.Combine(dir,"test-state.json");var a=new State {active=new Installation {generation=1,version="first"}};Core.AtomicState(statePath,a);
        var b=new State {active=new Installation {generation=2,version="second"},previous=a.active};Core.AtomicState(statePath,b);
        Require(Core.Json.Deserialize<State>(File.ReadAllText(statePath)).active.generation==2);
        Require(Core.Json.Deserialize<State>(File.ReadAllText(statePath+".backup")).active.generation==1);
        rejected=false;try{Core.ValidateInstallation(new Installation {folder=dir,build=Path.Combine(dir,"..","escape")});}catch{rejected=true;}Require(rejected);
        Require(Core.Json.Deserialize<State>(File.ReadAllText(statePath)).active.generation==2);
        var bad=Core.Json.Deserialize<Release>(Encoding.UTF8.GetString(data));bad.installer_url="http://example.com/file.exe";
        rejected=false;try{Core.Validate(bad);}catch{rejected=true;}Require(rejected);
        using(var worker=new Process {StartInfo=new ProcessStartInfo("powershell.exe","-NoProfile -NonInteractive -Command Start-Sleep -Seconds 30"){UseShellExecute=false,CreateNoWindow=true}}){
            using(var owned=new BuildJob()){worker.Start();owned.Attach(worker);owned.Dispose();Require(worker.WaitForExit(3000));}}
        File.WriteAllText(report,Core.Json.Serialize(new {status="passed",checks=count,game_launched=false,production_state_changed=false}));
    }
}
