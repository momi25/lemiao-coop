// Save selection uses Seamless's save_file_extension setting. Originals are
// read only; imported profiles live alongside the user's ordinary saves.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public class SaveProfile
{
    public string id {get;set;} public string label {get;set;}
    public string account {get;set;} public string extension {get;set;}
    public string source {get;set;}
    public override string ToString(){return label+"  ["+extension+"]";}
}
public sealed class SaveManager
{
    readonly string root,home;readonly Func<bool> running;readonly Func<string> activeAccount;
    public SaveManager(string saveRoot,string launcherHome,Func<bool> gameRunning,Func<string> steamAccount=null){root=Path.GetFullPath(saveRoot);home=launcherHome;running=gameRunning;activeAccount=steamAccount;}
    public void Guard(){if(running())throw new Exception("Quit Elden Ring through its menu before changing saves.");}
    public string[] Accounts(){return Directory.Exists(root)?Directory.GetDirectories(root).Select(Path.GetFileName).Where(x=>Regex.IsMatch(x,"^[0-9]{17}$")).OrderBy(x=>x).ToArray():new string[0];}
    public string ActiveAccount {get{return activeAccount==null?null:activeAccount();}}
    void CheckAccount(SaveProfile p){string active=ActiveAccount;if(!String.IsNullOrEmpty(active)&&active!=p.account)throw new Exception("Steam is signed in to a different account. Choose that account's save folder or switch accounts in Steam.");}
    public string Target(SaveProfile p){
        if(p==null||!Regex.IsMatch(p.account??"","^[0-9]{17}$")||!Regex.IsMatch(p.extension??"","^[A-Za-z0-9]{1,20}$")||
            p.extension.Equals("sl2",StringComparison.OrdinalIgnoreCase)||!Accounts().Contains(p.account))throw new Exception("Invalid save profile. Choose your own Steam save folder.");
        return Path.Combine(root,p.account,"ER0000."+p.extension);
    }
    public static string Extension(string ini){var m=Regex.Matches(ini,@"^[ \t]*save_file_extension[ \t]*=[ \t]*([A-Za-z0-9]+)[ \t]*(?:;[^\r\n]*)?\r?$",RegexOptions.Multiline);
        if(m.Count!=1)throw new Exception("Seamless settings must contain one save_file_extension entry.");return m[0].Groups[1].Value;}
    public static string SettingsFor(string ini,string extension){
        Extension(ini);if(!Regex.IsMatch(extension??"","^[A-Za-z0-9]{1,20}$")||extension.Equals("sl2",StringComparison.OrdinalIgnoreCase))throw new Exception("Invalid modded save extension.");
        return Regex.Replace(ini,@"^([ \t]*save_file_extension[ \t]*=[ \t]*)[A-Za-z0-9]+",m=>m.Groups[1].Value+extension,RegexOptions.Multiline);
    }
    public static void ValidateFile(string file){
        using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=new BinaryReader(stream)){
            if(stream.Length<1024*1024||stream.Length>128L*1024*1024||Encoding.ASCII.GetString(reader.ReadBytes(4))!="BND4")throw new Exception("This is not an Elden Ring save file.");
            stream.Position=12;if(reader.ReadInt32()!=12)throw new Exception("This save container has an unexpected format.");
        }
    }
    string Backup(string file){string folder=Path.Combine(home,"save-backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string copy=Path.Combine(folder,Path.GetFileName(file));File.Copy(file,copy,false);
        if(Core.Hash(copy)!=Core.Hash(file))throw new Exception("Save backup verification failed. Your selection was not changed.");
        File.WriteAllText(copy+".sha256",Core.Hash(copy));return copy;
    }
    public SaveProfile Import(string source,string account){
        Guard();source=Path.GetFullPath(source);ValidateFile(source);
        string parent=Path.GetFileName(Path.GetDirectoryName(source));
        if(Regex.IsMatch(parent,"^[0-9]{17}$")&&parent!=account)throw new Exception("This file is in another Steam account's folder. Select a save belonging to the chosen account.");
        var profile=new SaveProfile {id=Guid.NewGuid().ToString("N"),label=Path.GetFileName(source)+" / "+DateTime.Now.ToString("g"),account=account,source=source};
        CheckAccount(profile);
        // Unique four-character extensions keep every imported playthrough
        // separate. Neither an existing save nor its .bak is overwritten.
        for(int n=0;n<4096;n++){profile.extension="l"+n.ToString("x3");string target=Target(profile);
            if(File.Exists(target)||File.Exists(target+".bak"))continue;
            string snapshot=Backup(source);Guard();
            using(var input=File.OpenRead(snapshot))using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
            try {if(Core.Hash(target)!=Core.Hash(snapshot))throw new Exception("Imported save verification failed.");File.Copy(snapshot,target+".bak",false);ValidateFile(target);}
            catch{File.Delete(target);throw;}
            return profile;
        }
        throw new Exception("No unused save profile slot is available.");
    }
    public SaveProfile Current(string settings,string account){
        var p=new SaveProfile {id="existing-"+account+"-"+Extension(File.ReadAllText(settings)),label="Existing save",account=account,extension=Extension(File.ReadAllText(settings))};
        string path=Target(p);ValidateFile(path);p.source=path;return p;
    }
    static State Clone(State s){return Core.Json.Deserialize<State>(Core.Json.Serialize(s));}
    static void WriteSettings(string path,string text){string temp=path+".save-select-new";File.WriteAllText(temp,text,new UTF8Encoding(false));File.Replace(temp,path,null);}
    public State Select(State state,SaveProfile profile,string settings,string statePath){
        Guard();CheckAccount(profile);ValidateFile(Target(profile));string before=File.ReadAllText(settings);string after=SettingsFor(before,profile.extension);
        Backup(Target(profile));var next=Clone(state);if(next.save_profiles==null)next.save_profiles=new List<SaveProfile>();
        if(!next.save_profiles.Any(p=>p.id==profile.id))next.save_profiles.Add(profile);next.selected_save=profile.id;
        Guard();WriteSettings(settings,after);
        try {Core.AtomicState(statePath,next);}catch{WriteSettings(settings,before);throw;}
        return next;
    }
    public void Prepare(State state,string settings,bool backup){
        if(String.IsNullOrEmpty(state.selected_save))return;
        Guard();var profile=(state.save_profiles??new List<SaveProfile>()).FirstOrDefault(p=>p.id==state.selected_save);
        if(profile==null)throw new Exception("The selected save profile is missing. Choose a save before playing.");
        CheckAccount(profile);
        string target=Target(profile);ValidateFile(target);
        // Never re-import the original: this file contains the latest progress.
        if(backup)Backup(target);
        string ini=File.ReadAllText(settings),after=SettingsFor(ini,profile.extension);Guard();if(after!=ini)WriteSettings(settings,after);
    }
}

public sealed class SaveBrowser:Form
{
    readonly SaveManager manager;readonly State state;readonly string settings;
    ComboBox accounts,profiles;Label path;Button use;string pendingSource;public SaveProfile Selected {get;private set;}public SaveProfile Existing {get;private set;}
    public SaveBrowser(SaveManager saves,State current,string settingsPath){
        manager=saves;state=current;settings=settingsPath;Text="Choose your save";ClientSize=new Size(650,350);FormBorderStyle=FormBorderStyle.FixedDialog;
        MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);
        BackColor=Color.FromArgb(24,31,31);ForeColor=Color.FromArgb(237,235,225);
        Controls.Add(new Label {Text="Choose a save belonging to your Steam account. Imports are copied once;\nfuture progress stays in that profile. Originals are kept unchanged.",Location=new Point(24,20),Size=new Size(600,52)});
        Controls.Add(new Label {Text="Steam save folder",Location=new Point(24,87),Size=new Size(170,25)});
        accounts=new ComboBox {Location=new Point(210,83),Size=new Size(414,28),DropDownStyle=ComboBoxStyle.DropDownList};Controls.Add(accounts);
        accounts.Items.AddRange(manager.Accounts());accounts.SelectedIndexChanged+=(s,e)=>Populate();
        profiles=new ComboBox {Location=new Point(24,132),Size=new Size(600,30),DropDownStyle=ComboBoxStyle.DropDownList};Controls.Add(profiles);
        profiles.SelectedIndexChanged+=(s,e)=>{pendingSource=null;Selected=profiles.SelectedItem as SaveProfile;path.Text=Selected==null?"Browse to import a save.":manager.Target(Selected);use.Enabled=Selected!=null;};
        path=new Label {Location=new Point(24,180),Size=new Size(600,65),ForeColor=Color.FromArgb(160,170,164)};Controls.Add(path);
        var browse=new Button {Text="BROWSE / IMPORT...",Location=new Point(24,260),Size=new Size(210,45)};browse.Click+=(s,e)=>Browse();Controls.Add(browse);
        use=new Button {Text="USE THIS SAVE",Location=new Point(394,260),Size=new Size(230,45),Enabled=false};
        use.Click+=(s,e)=>{try{manager.Guard();if(pendingSource!=null){Selected=manager.Import(pendingSource,(string)accounts.SelectedItem);pendingSource=null;}
            SaveManager.ValidateFile(manager.Target(Selected));DialogResult=DialogResult.OK;Close();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Save selection");}};Controls.Add(use);
        Controls.Add(new Label {Text="Switching does not start the game. Choose characters through Load Game.",Location=new Point(24,319),Size=new Size(600,24),Font=new Font("Segoe UI",9)});
        var selected=(state.save_profiles??new List<SaveProfile>()).FirstOrDefault(p=>p.id==state.selected_save);
        if(accounts.Items.Count!=0){string preferred=selected==null?manager.ActiveAccount:selected.account;int index=preferred==null?-1:accounts.Items.IndexOf(preferred);accounts.SelectedIndex=index<0?0:index;}
        else{browse.Enabled=false;path.Text="No Steam save folder found. Choose Save after your own account has an Elden Ring save.";}
    }
    void Populate(){profiles.Items.Clear();Selected=null;Existing=null;pendingSource=null;use.Enabled=false;string account=(string)accounts.SelectedItem;
        foreach(var p in state.save_profiles??new List<SaveProfile>())if(p.account==account)profiles.Items.Add(p);
        try{var existing=manager.Current(settings,account);Existing=profiles.Items.Cast<SaveProfile>().FirstOrDefault(p=>p.extension==existing.extension)??existing;
            if(!profiles.Items.Cast<SaveProfile>().Any(p=>p.extension==Existing.extension))profiles.Items.Insert(0,Existing);}catch{}
        int index=profiles.Items.Cast<SaveProfile>().ToList().FindIndex(p=>p.id==state.selected_save);if(profiles.Items.Count!=0)profiles.SelectedIndex=index<0?0:index;else path.Text="Browse to import a save.";
    }
    void Browse(){try{manager.Guard();using(var dialog=new OpenFileDialog {Title="Choose your Elden Ring save",Filter="Elden Ring saves|*.sl2;*.co2;*.rch4;*.rcl3;*.rco3;*.bak|All files|*.*",InitialDirectory=Path.GetDirectoryName(manager.Target(new SaveProfile {account=(string)accounts.SelectedItem,extension="rch4"}))}){
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;SaveManager.ValidateFile(dialog.FileName);profiles.SelectedIndex=-1;
        pendingSource=dialog.FileName;Selected=null;path.Text="Import a separate copy of:\n"+pendingSource;use.Enabled=true;
    }}catch(Exception ex){MessageBox.Show(this,ex.Message,"Save import");}}
}

static class SaveTests
{
    static void FakeSave(string path,byte marker){Directory.CreateDirectory(Path.GetDirectoryName(path));using(var f=new FileStream(path,FileMode.Create,FileAccess.Write)){
        f.SetLength(1024*1024);f.Write(Encoding.ASCII.GetBytes("BND4"),0,4);f.Position=12;f.Write(BitConverter.GetBytes(12),0,4);f.Position=64;f.WriteByte(marker);}}
    public static void Run(string qa,Action<bool> require){
        string dir=Path.Combine(qa,"save-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        string root=Path.Combine(dir,"EldenRing"),home=Path.Combine(dir,"Launcher"),account="76561198000000001",other="76561198000000002";
        Directory.CreateDirectory(Path.Combine(root,account));Directory.CreateDirectory(Path.Combine(root,other));bool running=false;
        var manager=new SaveManager(root,home,()=>running);string source=Path.Combine(dir,"ER0000.sl2");FakeSave(source,11);string originalHash=Core.Hash(source);
        string ordinary=Path.Combine(root,account,"ER0000.rch4");FakeSave(ordinary,22);string ordinaryHash=Core.Hash(ordinary);
        // Never overwrite an unrelated pre-existing profile or recovery file.
        File.WriteAllText(Path.Combine(root,account,"ER0000.l000.bak"),"reserved");
        var imported=manager.Import(source,account);require(imported.extension=="l001");require(Core.Hash(source)==originalHash);
        require(Core.Hash(manager.Target(imported))==originalHash);require(Core.Hash(manager.Target(imported)+".bak")==originalHash);require(Core.Hash(ordinary)==ordinaryHash);
        string settings=Path.Combine(dir,"ersc_settings.ini"),statePath=Path.Combine(home,"state.json");
        const string ini="[NETWORK]\r\ncooppassword = private-test\r\n[SAVE]\r\nsave_file_extension = rch4 ; keep comment\r\n";File.WriteAllText(settings,ini);
        var current=manager.Current(settings,account);var state=new State {game="fixture",save_profiles=new List<SaveProfile>{current},active=new Installation {generation=1}};
        state=manager.Select(state,imported,settings,statePath);require(state.selected_save==imported.id);require(state.save_profiles.Count==2);
        require(SaveManager.Extension(File.ReadAllText(settings))==imported.extension);require(File.ReadAllText(settings).Contains("cooppassword = private-test"));require(File.ReadAllText(settings).Contains("; keep comment"));
        state=Core.Json.Deserialize<State>(File.ReadAllText(statePath));require(state.selected_save==imported.id);
        // Simulate game progress. Prepare and an update must not restore the
        // original imported snapshot, or replace the user's vanilla save.
        FakeSave(manager.Target(imported),33);string progressHash=Core.Hash(manager.Target(imported));
        manager.Prepare(state,settings,true);require(Core.Hash(manager.Target(imported))==progressHash);require(Core.Hash(source)==originalHash);
        var update=state.WithInstallations(new Installation {generation=2},state.active,false);require(update.selected_save==imported.id&&update.save_profiles.Count==2);
        File.WriteAllText(settings,ini);manager.Prepare(update,settings,false);require(SaveManager.Extension(File.ReadAllText(settings))==imported.extension);require(Core.Hash(manager.Target(imported))==progressHash);
        var rollback=update.WithInstallations(update.previous,update.active,true);require(rollback.selected_save==imported.id&&rollback.updates_paused);
        state=manager.Select(state,current,settings,statePath);require(SaveManager.Extension(File.ReadAllText(settings))=="rch4");require(Core.Hash(ordinary)==ordinaryHash);
        state=manager.Select(state,imported,settings,statePath);require(Core.Hash(manager.Target(imported))==progressHash);
        // All running-game paths reject changes, including selection/import.
        running=true;bool blocked=false;string beforeSettings=File.ReadAllText(settings),beforeState=File.ReadAllText(statePath);
        try{manager.Import(source,account);}catch{blocked=true;}require(blocked);blocked=false;
        try{manager.Select(state,current,settings,statePath);}catch{blocked=true;}require(blocked);blocked=false;
        try{manager.Prepare(state,settings,true);}catch{blocked=true;}require(blocked);
        require(beforeSettings==File.ReadAllText(settings)&&beforeState==File.ReadAllText(statePath));running=false;
        // A failed state commit restores the old settings and selection.
        string failure=Path.Combine(dir,"blocked-state");Directory.CreateDirectory(failure);blocked=false;
        try{manager.Select(state,current,settings,Path.Combine(failure,"..","blocked-state"));}catch{blocked=true;}require(blocked);require(File.ReadAllText(settings)==beforeSettings);
        string invalid=Path.Combine(dir,"not-a-save.sl2");File.WriteAllText(invalid,"invalid");blocked=false;try{manager.Import(invalid,account);}catch{blocked=true;}require(blocked);
        blocked=false;try{manager.Target(new SaveProfile {account="..",extension="sl2"});}catch{blocked=true;}require(blocked);
        blocked=false;try{SaveManager.SettingsFor(ini,"sl2");}catch{blocked=true;}require(blocked);
        string foreign=Path.Combine(root,other,"ER0000.sl2");FakeSave(foreign,44);blocked=false;try{manager.Import(foreign,account);}catch{blocked=true;}require(blocked);
        string missing=manager.Target(imported);File.Move(missing,missing+".moved");blocked=false;try{manager.Prepare(state,settings,false);}catch{blocked=true;}require(blocked);
        require(Directory.GetFiles(Path.Combine(home,"save-backups"),"*.sha256",SearchOption.AllDirectories).Length>=4);
        require(Core.Hash(source)==originalHash&&Core.Hash(ordinary)==ordinaryHash);
        var wrongSteam=new SaveManager(root,home,()=>false,()=>other);blocked=false;try{wrongSteam.Select(state,current,settings,statePath);}catch{blocked=true;}require(blocked);
        using(var browser=new SaveBrowser(manager,new State(),settings)){require(browser.Controls.OfType<ComboBox>().Count()==2);}
    }
}
