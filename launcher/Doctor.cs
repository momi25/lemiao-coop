// Local PC checks. No game launch, save contents or automatic report upload.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;

public class PCCheck
{
    public string name {get;set;} public string status {get;set;} public string detail {get;set;} public string fix {get;set;}
}

public sealed class PCResults:Form
{
    public PCResults(PCReport report){Text="Lemiao / This PC";ClientSize=new Size(840,520);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
        BackColor=Color.FromArgb(24,31,31);ForeColor=Color.FromArgb(237,235,225);
        Controls.Add(new Label {Text=report.summary,Location=new Point(20,16),Size=new Size(800,45)});
        var table=new DataGridView {Location=new Point(20,73),Size=new Size(800,335),ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
            RowHeadersVisible=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=Color.FromArgb(24,31,31),AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill};
        table.DefaultCellStyle.WrapMode=DataGridViewTriState.True;table.Columns.Add("status","Result");table.Columns[0].FillWeight=14;
        table.Columns.Add("check","Check");table.Columns[1].FillWeight=31;table.Columns.Add("detail","Details / next step");table.Columns[2].FillWeight=75;
        foreach(var check in report.checks){int i=table.Rows.Add(check.status.ToUpperInvariant(),check.name,check.detail+(String.IsNullOrEmpty(check.fix)?"":"\n"+check.fix));
            table.Rows[i].DefaultCellStyle.ForeColor=check.status=="fail"?Color.DarkRed:check.status=="warn"?Color.DarkGoldenrod:Color.FromArgb(20,80,45);}
        Controls.Add(table);
        var export=new Button {Text="EXPORT REPORT...",Location=new Point(20,428),Size=new Size(220,45)};Controls.Add(export);
        export.Click+=(s,e)=>{using(var dialog=new SaveFileDialog {Title="Save a PC diagnostics report",Filter="JSON report|*.json",FileName="Lemiao-PC-check.json"}){
            if(dialog.ShowDialog(this)==DialogResult.OK){try{File.WriteAllText(dialog.FileName,Core.Json.Serialize(report),new UTF8Encoding(false));}
                catch(Exception ex){MessageBox.Show(this,ex.Message,"Report export");}}}};
        var close=new Button {Text="CLOSE",Location=new Point(650,428),Size=new Size(170,45)};close.Click+=(s,e)=>Close();Controls.Add(close);
        Controls.Add(new Label {Text="No game launched. No save contents, passwords or automatic uploads. Gameplay still needs a live test.",Location=new Point(20,483),Size=new Size(800,25),Font=new Font("Segoe UI",9)});
    }
}

static class DoctorTests
{
    static void PE(string path,bool x64){Directory.CreateDirectory(Path.GetDirectoryName(path));var data=new byte[512];data[0]=0x4d;data[1]=0x5a;
        BitConverter.GetBytes(128).CopyTo(data,60);BitConverter.GetBytes(0x4550).CopyTo(data,128);BitConverter.GetBytes((ushort)(x64?0x8664:0x14c)).CopyTo(data,132);File.WriteAllBytes(path,data);}
    public static void Run(string qa,Action<bool> require){
        string root=Path.Combine(qa,"PC fixture è 日本 "+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"Different Steam library","Game"),home=Path.Combine(root,"Private tools");
        Directory.CreateDirectory(game);PE(Path.Combine(game,"eldenring.exe"),true);PE(Path.Combine(game,"SeamlessCoop","ersc.dll"),true);PE(Path.Combine(game,"oo2core_6_win64.dll"),true);
        File.WriteAllText(Path.Combine(game,"regulation.bin"),"authored test fixture");
        string settings=Path.Combine(game,"SeamlessCoop","ersc_settings.ini");File.WriteAllText(settings,"save_file_extension = co2\ncooppassword = MUST_NOT_EXPORT\n");
        foreach(string name in new[]{"Data0","Data1","Data2","Data3"})foreach(string ext in new[]{".bhd",".bdt"})File.WriteAllText(Path.Combine(game,name+ext),"fixture");
        var reference=new Dictionary<string,object>{{"game_exe_sha256",Core.Hash(Path.Combine(game,"eldenring.exe"))},{"game_regulation_sha256",Core.Hash(Path.Combine(game,"regulation.bin"))},{"seamless_dll_sha256",Core.Hash(Path.Combine(game,"SeamlessCoop","ersc.dll"))}};
        var r=new PCReport();PCDoctor.ScanFiles(r,game,reference);require(r.compatible);require(PCDoctor.X64File(Path.Combine(game,"eldenring.exe")));
        require(!Core.Json.Serialize(r).Contains("MUST_NOT_EXPORT"));require(!Core.Json.Serialize(r).Contains(root));require(!r.game_launched&&!r.gameplay_verified&&!r.save_changes);
        PCDoctor.ProbeFolder(r,home);require(r.compatible);require(Directory.GetDirectories(home,"pc-check-*").Length==0);
        string dll=Path.Combine(game,"SeamlessCoop","ersc.dll");File.Move(dll,dll+".fixture-moved");r=new PCReport();PCDoctor.ScanFiles(r,game,reference);require(!r.compatible&&r.checks.Any(c=>c.name=="Seamless version"&&c.status=="fail"));File.Move(dll+".fixture-moved",dll);
        File.WriteAllText(Path.Combine(game,"regulation.bin"),"modified by another mod");r=new PCReport();PCDoctor.ScanFiles(r,game,reference);require(!r.compatible&&r.checks.Any(c=>c.name=="Vanilla regulation"&&c.status=="fail"));
        File.WriteAllText(Path.Combine(game,"regulation.bin"),"authored test fixture");PE(Path.Combine(game,"oo2core_6_win64.dll"),false);r=new PCReport();PCDoctor.ScanFiles(r,game,reference);
        require(!r.compatible&&r.checks.Any(c=>c.name=="Game decompressor"&&c.status=="fail"));PE(Path.Combine(game,"oo2core_6_win64.dll"),true);
        File.WriteAllText(settings,"save_file_extension = sl2\n");r=new PCReport();PCDoctor.ScanFiles(r,game,reference);require(!r.compatible);
        File.WriteAllBytes(settings,new byte[]{0x85,0xff});r=new PCReport();PCDoctor.ScanFiles(r,game,reference);require(!r.compatible&&r.checks.Any(c=>c.name=="Seamless settings"&&c.status=="fail"));
        File.WriteAllText(settings,"save_file_extension = co2\n");File.Delete(Path.Combine(game,"Data2.bdt"));r=new PCReport();PCDoctor.ScanFiles(r,game,reference);require(!r.compatible&&r.checks.Any(c=>c.name=="Owner game archives"&&c.status=="fail"));
        r=new PCReport();PCDoctor.ScanFiles(r,Path.Combine(root,"not installed"),reference);require(!r.compatible&&r.Problem.Contains("Game folder"));
        using(var form=new PCResults(r)){require(form.Controls.OfType<DataGridView>().Single().Rows.Count==r.checks.Count);}
        string denied=Path.Combine(root,"file instead of folder");File.WriteAllText(denied,"fixture");r=new PCReport();PCDoctor.ProbeFolder(r,denied);require(!r.compatible);
    }
}
public class PCReport
{
    public int schema=1;public string launcher="1.3";public string checked_utc=DateTime.UtcNow.ToString("u");
    public string windows_language=CultureInfo.InstalledUICulture.Name;public int process_bits=IntPtr.Size*8;
    public bool game_launched=false,save_changes=false,gameplay_verified=false,installation_verified=false;
    public string gameplay_fingerprint=null;
    public List<PCCheck> checks=new List<PCCheck>();
    public bool compatible {get{return !checks.Any(x=>x.status=="fail");}}
    public string summary {get{return compatible?(installation_verified?"PC checks and installed gameplay files passed. Gameplay has not been tested.":"PC setup checks passed. Installation still needs to finish."):"PC checks found a problem. See the failed checks and suggested fixes.";}}
    public void Add(string name,string status,string detail,string fix=null){checks.Add(new PCCheck {name=name,status=status,detail=detail,fix=fix});}
    public string Problem {get{var problem=checks.FirstOrDefault(x=>x.status=="fail");return problem==null?summary:problem.name+": "+problem.detail+" "+problem.fix;}}
}
public static class PCDoctor
{
    const string BaselineReg="766521f9508de3a3532df61c45a1c2d93340f1ff7ed8306ab20df761712ca2ab";
    public static Dictionary<string,object> Reference(){using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("reference.json"))
        using(var reader=new StreamReader(resource))return Core.Json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());}
    public static bool X64File(string path){using(var input=File.OpenRead(path))using(var reader=new BinaryReader(input)){
        if(input.Length<64||reader.ReadUInt16()!=0x5a4d)return false;input.Position=60;uint offset=reader.ReadUInt32();
        if(offset>input.Length-6)return false;input.Position=offset;return reader.ReadUInt32()==0x4550&&reader.ReadUInt16()==0x8664;}}
    static void FileHash(PCReport r,string game,string relative,string expected,string name,string fix){try{
        string file=Path.Combine(game,relative);if(!File.Exists(file)){r.Add(name,"fail","Required file is missing.",fix);return;}
        string actual=Core.Hash(file);r.Add(name,actual==expected?"pass":"fail",actual==expected?"Matches the supported release.":"File SHA256 differs: "+actual,actual==expected?null:fix);
    }catch{r.Add(name,"fail","The file could not be read.","Close programs locking this file and check folder access.");}}
    public static void ScanFiles(PCReport r,string game,Dictionary<string,object> reference){
        if(String.IsNullOrEmpty(game)||!Directory.Exists(game)){r.Add("Game folder","fail","Elden Ring was not found.","Select eldenring.exe in your Steam library when the launcher asks.");return;}
        FileHash(r,game,"eldenring.exe",(string)reference["game_exe_sha256"],"Elden Ring version","This build supports the inspected worldwide 1.17.1 release. Use Steam to verify the supported game files; a newer version needs a mod update.");
        FileHash(r,game,"regulation.bin",reference.ContainsKey("game_regulation_sha256")?(string)reference["game_regulation_sha256"]:BaselineReg,"Vanilla regulation","Restore the game's original regulation.bin through Steam verification. The launcher keeps mods in a separate folder.");
        FileHash(r,game,Path.Combine("SeamlessCoop","ersc.dll"),(string)reference["seamless_dll_sha256"],"Seamless version","Install the same supported Seamless Co-op version on both PCs.");
        try{string library=Path.Combine(game,"oo2core_6_win64.dll");bool found=File.Exists(library)&&X64File(library);r.Add("Game decompressor",found?"pass":"fail",found?"The owner's x64 Oodle library is present.":"The owner's x64 Oodle library is missing or invalid.",found?null:"Verify the game files through Steam if it is missing or damaged.");}
        catch{r.Add("Game decompressor","fail","The library could not be inspected.","Check access to the game's files.");}
        try{string settings=File.ReadAllText(Path.Combine(game,"SeamlessCoop","ersc_settings.ini"),new UTF8Encoding(false,true));
            string extension=SaveManager.Extension(settings);bool valid=Regex.IsMatch(extension,"^[A-Za-z0-9]{1,20}$")&&!extension.Equals("sl2",StringComparison.OrdinalIgnoreCase);
            r.Add("Seamless settings",valid?"pass":"fail","Save extension settings checked; passwords are excluded from this report.",valid?null:"Use a modded save extension such as co2, not sl2.");
        }catch{r.Add("Seamless settings","fail","Settings are missing, invalid, or not readable as UTF-8.","Restore ersc_settings.ini from the supported Seamless download, then set your party password.");}
        bool archives=true;foreach(string archive in new[]{"Data0","Data1","Data2","Data3"})foreach(string ext in new[]{".bhd",".bdt"}){
            string file=Path.Combine(game,archive+ext);if(!File.Exists(file)||new FileInfo(file).Length==0)archives=false;}
        r.Add("Owner game archives",archives?"pass":"fail",archives?"Required base archives are present.":"One or more base archives are missing or empty.",archives?null:"Verify the game files through Steam. Extracted assets are built from your own installation.");
    }
    public static void ProbeFolder(PCReport r,string home){string folder=Path.Combine(home,"pc-check-"+Guid.NewGuid().ToString("N"));string file=Path.Combine(folder,"prova è 日本.txt");
        try{if(!Core.Within(home,folder))throw new IOException();Directory.CreateDirectory(folder);File.WriteAllText(file,"UTF-8: è 日本",new UTF8Encoding(false));
            if(File.ReadAllText(file)!="UTF-8: è 日本")throw new IOException();r.Add("Folder permissions / Unicode","pass","Temporary write, read and cleanup checks passed with a non-English filename.");}
        catch{r.Add("Folder permissions / Unicode","fail","The launcher cannot write to its private folder.","Check Windows folder permissions or security software blocking this app.");}
        finally{try{if(File.Exists(file))File.Delete(file);if(Directory.Exists(folder))Directory.Delete(folder,false);}catch{r.Add("Temporary cleanup","warn","A small PC-check temporary file could not be removed.");}}
    }
    static bool Tool(string exe,string args,string expected){var text=new StringBuilder();var start=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
        StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        start.EnvironmentVariables["PYTHONUTF8"]="1";start.EnvironmentVariables["OPENBLAS_NUM_THREADS"]="1";start.EnvironmentVariables["OMP_NUM_THREADS"]="1";
        using(var process=new Process {StartInfo=start})using(var owned=new BuildJob()){
            process.OutputDataReceived+=(s,e)=>{if(e.Data!=null)lock(text)text.AppendLine(e.Data);};process.ErrorDataReceived+=(s,e)=>{};
            process.Start();owned.Attach(process);process.BeginOutputReadLine();process.BeginErrorReadLine();
            if(!process.WaitForExit(20000))return false;process.WaitForExit();return process.ExitCode==0&&text.ToString().Contains(expected);}
    }
    static void Tools(PCReport r,string folder){
        string root=Path.Combine(folder,"Remnant-Maliketh-Character-Test","local"),python=Path.Combine(root,"venv","Scripts","python.exe");
        if(!File.Exists(python)){r.Add("Private build tools","info","Private tools have not been installed here yet. Setup downloads its own pinned tools; system Python is not required.");return;}
        try{string code="import io,sys,numpy,zstandard,soulstruct,constrata;from Crypto.Cipher import AES;from PIL import Image;assert sys.version_info[:2]==(3,11);AES.new(bytes(16),AES.MODE_ECB).encrypt(bytes(16));assert zstandard.ZstdDecompressor().decompress(zstandard.ZstdCompressor().compress(b'probe'))==b'probe';Image.new('RGBA',(1,1)).save(io.BytesIO(),format='PNG');print('LEMIAO_TOOLS_OK')";
            bool ok=Tool(python,"-X utf8 -c "+Core.Quote(code),"LEMIAO_TOOLS_OK");r.Add("Private Python / native dependencies",ok?"pass":"fail",ok?"Python 3.11, NumPy, AES, Zstandard and image codecs executed successfully.":"A private tool failed, timed out or was blocked.",ok?null:"Use Check Updates to rebuild private tools. System Python is not needed; inspect Windows security history if execution was blocked.");
        }catch{r.Add("Private Python / native dependencies","fail","The private interpreter could not run.","Use Check Updates and check whether Windows security blocked the private runtime.");}
        try{bool ok=Tool(Path.Combine(root,"dotnet","dotnet.exe"),"--list-runtimes","Microsoft.NETCore.App 9.0.20");r.Add("Private .NET runtime",ok?"pass":"fail",ok?"The pinned .NET 9.0.20 runtime runs.":"The private runtime could not run.",ok?null:"Use Check Updates to rebuild the private runtime.");}
        catch{r.Add("Private .NET runtime","fail","The private runtime could not be started.","Use Check Updates to rebuild the private runtime.");}
    }
    public static PCReport Run(State state,string home,bool network,bool tools,bool inspectInstallation=true){var r=new PCReport();
        bool platform=Environment.OSVersion.Platform==PlatformID.Win32NT&&Environment.Is64BitOperatingSystem&&IntPtr.Size==8;
        r.Add("Windows / architecture",platform?"pass":"fail",platform?"64-bit Windows and x64 launcher confirmed.":"This package requires x64 Windows.",platform?null:"Use an x64 Windows PC with the supported Elden Ring installation.");
        r.Add("Launcher framework","pass","CLR "+Environment.Version+" loaded successfully.");
        bool steam=Process.GetProcessesByName("steam").Length!=0;r.Add("Steam",steam?"pass":"warn",steam?"Steam is running. Sign-in is still required to play.":"Steam is not running.",steam?null:"Open Steam and sign in before playing.");
        ScanFiles(r,state.game,Reference());ProbeFolder(r,home);
        try{long free=new DriveInfo(Path.GetPathRoot(home)).AvailableFreeSpace;bool enough=free>=3L*1024*1024*1024;
            r.Add("Build disk space",enough?"pass":"fail",(free/(1024L*1024*1024))+" GiB available; at least 3 GiB reserved for a fresh build.",enough?null:"Free space on the drive containing the launcher's private folder, then retry.");}
        catch{r.Add("Build disk space","warn","Available disk space could not be measured.");}
        if(Core.GameRunning())r.Add("Running game","warn","An existing game session was detected. Updates and save switching wait until it closes.","Quit through the game menu before installing an update.");
        if(inspectInstallation&&state.active!=null){try{Core.ValidateInstallation(state.active);r.installation_verified=true;r.Add("Installed gameplay files","pass","All authored gameplay files and the controller match the release hashes.");
                FileHash(r,state.active.build,Path.Combine("SeamlessCoop","ersc.dll"),(string)Reference()["seamless_dll_sha256"],"Installed Seamless copy","Use Check Updates to repair the private installation.");
                string script=File.ReadAllText(Path.Combine(state.active.build,"Launch-Maliketh-Coop.ps1"));
                var loader=Regex.Match(script,@"try \{ & '((?:[^']|'')*)' -t er -c");
                bool valid=loader.Success&&File.Exists(loader.Groups[1].Value.Replace("''","'"))&&X64File(loader.Groups[1].Value.Replace("''","'"));
                r.Add("Mod Engine launch profile",valid?"pass":"fail",valid?"The referenced x64 Mod Engine loader is present. It was not executed.":"The launch profile references a missing or invalid loader.",valid?null:"Use Check Updates to rebuild the local launch profile.");
            }catch{r.installation_verified=false;r.Add("Installed gameplay files","fail","Installation is incomplete, modified or unreadable.","Use Check Updates to build a fresh private installation.");}}
        else r.Add("Installation","info",state.active==null?"Setup has not completed yet. Play stays disabled until the final file checks pass.":"The existing installation is verified when checking this PC or pressing Play.");
        if(r.installation_verified){var reference=Reference();var files=(Dictionary<string,object>)reference["gameplay_files"];
            string contents=(string)reference["controller_sha256"]+"\n"+String.Join("\n",files.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+":"+(string)x.Value));
            using(var hash=SHA256.Create())r.gameplay_fingerprint=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(contents))).Replace("-","").ToLowerInvariant();
            r.Add("Party gameplay match key","pass",r.gameplay_fingerprint.Substring(0,16)+" — both players' verified reports should show the same key.");
            try{string ini=File.ReadAllText(Path.Combine(state.active.build,"SeamlessCoop","ersc_settings.ini"),new UTF8Encoding(false,true));
                var password=Regex.Match(ini,@"^[ \t]*cooppassword[ \t]*=[ \t]*([^\r\n]*)",RegexOptions.Multiline);bool set=password.Success&&!String.IsNullOrWhiteSpace(password.Groups[1].Value);
                r.Add("Party password",set?"pass":"warn",set?"A private password is configured. Its value is not exported.":"No party password is configured.",set?null:"Use Co-op Settings and enter the same password on both PCs.");
            }catch{r.Add("Party password","warn","Private party settings could not be checked.","Open Co-op Settings before joining your friend.");}}
        if(inspectInstallation&&state.active==null&&!String.IsNullOrEmpty(state.last_error))r.Add("Last setup attempt","fail","The previous installation did not finish; no working build is selected.","Press Check Updates to retry. View Log contains the private detailed error.");
        if(tools){string folder=state.active==null?state.last_attempt_folder:state.active.folder;if(!String.IsNullOrEmpty(folder)&&Directory.Exists(folder))Tools(r,folder);}
        if(network){try{var release=Core.ReadFeed();r.Add("Signed update feed","pass","Signature verified; current generation "+release.generation+" / "+release.version);}
            catch{r.Add("Signed update feed","warn","The signed feed could not be verified or reached.","Retry Check Updates. Your installed build is retained; inspect network/proxy restrictions if downloads fail.");}
            foreach(var endpoint in new[]{new[]{"Python download server","https://api.nuget.org/v3-flatcontainer/python/3.11.9/python.3.11.9.nupkg"},new[]{"Python package server","https://pypi.org/simple/soulstruct/"},new[]{"Pinned resource server","https://raw.githubusercontent.com/Nordgaren/UXM-Selective-Unpack/9501be87e272b6dae55e60a13c3f4753ca6fb3bb/UXM/ArchiveKeys.cs"}}){
                try{var request=(HttpWebRequest)WebRequest.Create(endpoint[1]);request.Method="HEAD";request.Timeout=8000;request.UserAgent="LemiaoPCCheck/1.3";
                    using(var response=(HttpWebResponse)request.GetResponse())r.Add(endpoint[0],"pass","HTTPS download endpoint reachable.");}
                catch{r.Add(endpoint[0],"warn","Download endpoint could not be reached.","Check Internet/proxy restrictions if setup cannot download its tools.");}}
        }
        Directory.CreateDirectory(home);File.WriteAllText(Path.Combine(home,"pc-check.json"),Core.Json.Serialize(r),new UTF8Encoding(false));return r;
    }
}
