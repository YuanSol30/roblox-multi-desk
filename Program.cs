using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace MultiDesk {
public class Profile { public string Id; public string Name; public override string ToString() { return Name; } }
public class Settings { public string Player; public List<Profile> Profiles = new List<Profile>(); }
public class SavedValue { public string Path; public string Name; public bool Exists; public string Value; public int Kind; }
public class Backup { public string Command; public List<SavedValue> Values = new List<SavedValue>(); public List<string> Created = new List<string>(); }
static class Disk {
 public static string Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
 public static T Read<T>(string path) where T:new() { if(!File.Exists(path)) return new T(); using(var f=File.OpenRead(path)) return (T)new XmlSerializer(typeof(T)).Deserialize(f); }
 public static void Write<T>(string path,T data) { Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp=path+".tmp"; using(var f=File.Create(temp)) new XmlSerializer(typeof(T)).Serialize(f,data); if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path); }
}
static class Links {
 public static bool Launch(string s) { return s!=null && s.Length<32700 && s.StartsWith("roblox-player:",StringComparison.OrdinalIgnoreCase) && s.Length>14 && !s.Any(c=>char.IsControl(c)||c=='"'||c=='\\'); }
 public static string Quote(string s) { var b=new StringBuilder("\"");int slashes=0;foreach(char c in s){if(c=='\\'){slashes++;continue;}if(c=='"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);return b.Append('"').ToString(); }
}
// Only these three protocol values are changed. A durable backup precedes any change.
sealed class Protocol {
 readonly string root, file;
 public Protocol(string key,string backupFile) { root=key; file=backupFile; }
 public bool Pending { get { return File.Exists(file); } }
 public void Enable(string exe) {
  if(Pending) Restore();
  var b=new Backup { Command=Links.Quote(exe)+" --relay \"%1\"" };
  foreach(string p in new[]{root,root+@"\shell",root+@"\shell\open",root+@"\shell\open\command"}) using(var k=Registry.CurrentUser.OpenSubKey(p)) if(k==null) b.Created.Add(p);
  foreach(var pair in new[]{new[]{root,""},new[]{root,"URL Protocol"},new[]{root+@"\shell\open\command",""}}) {
   var v=new SavedValue {Path=pair[0],Name=pair[1]}; using(var k=Registry.CurrentUser.OpenSubKey(v.Path)) { v.Exists=k!=null&&k.GetValueNames().Contains(v.Name); if(v.Exists) { var value=k.GetValue(v.Name,null,RegistryValueOptions.DoNotExpandEnvironmentNames); if(!(value is string)) throw new Exception("The existing Roblox protocol has an unusual format. It was left unchanged."); v.Value=(string)value; v.Kind=(int)k.GetValueKind(v.Name); } } b.Values.Add(v);
  }
  Disk.Write(file,b);
  try { using(var k=Registry.CurrentUser.CreateSubKey(root)) {k.SetValue("","URL:Roblox Player"); k.SetValue("URL Protocol","");} using(var k=Registry.CurrentUser.CreateSubKey(root+@"\shell\open\command")) k.SetValue("",b.Command); }
  catch { Restore(); throw; }
 }
 public void Restore() {
  if(!Pending) return; var b=Disk.Read<Backup>(file);
  // Do not overwrite a newer handler installed by a Roblox update or another app.
  using(var k=Registry.CurrentUser.OpenSubKey(root+@"\shell\open\command")) { string current=k==null?null:k.GetValue("") as string; var old=b.Values.Last(); if(current!=null && current!=b.Command && (!old.Exists || current!=old.Value)) throw new Exception("Roblox's link handler changed after enabling Multi Desk. The backup is retained in Data. Close Multi Desk and use Roblox's installer to repair its handler if needed."); }
  foreach(var v in b.Values) using(var k=Registry.CurrentUser.CreateSubKey(v.Path)) {if(v.Exists) k.SetValue(v.Name,v.Value,(RegistryValueKind)v.Kind); else k.DeleteValue(v.Name,false);}
  foreach(string p in b.Created.AsEnumerable().Reverse()) {bool empty; using(var k=Registry.CurrentUser.OpenSubKey(p)) empty=k!=null&&k.ValueCount==0&&k.SubKeyCount==0; if(empty) Registry.CurrentUser.DeleteSubKey(p,false);}
  File.Delete(file);
 }
}
static class Native {
 [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr h,int info,IntPtr b,int size,out int needed);
 [DllImport("ntdll.dll")] static extern int NtQueryObject(IntPtr h,int info,IntPtr b,int size,out int needed);
 [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool DuplicateHandle(IntPtr source,IntPtr value,IntPtr target,out IntPtr copy,uint access,bool inherit,uint options);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateFile(string path,uint access,uint share,IntPtr sa,uint disposition,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool DeviceIoControl(IntPtr h,uint code,byte[] input,int size,IntPtr output,int outputSize,out int returned,IntPtr overlapped);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int cmd);
 static string ObjectText(IntPtr h,int info) { int n; IntPtr b=Marshal.AllocHGlobal(8192); try {if(NtQueryObject(h,info,b,8192,out n)<0) return null; int len=(ushort)Marshal.ReadInt16(b); IntPtr p=Marshal.ReadIntPtr(b,8); return p==IntPtr.Zero?null:Marshal.PtrToStringUni(p,len/2);} finally {Marshal.FreeHGlobal(b);} }
 public static int ClearLocks(int pid,string prefix) {
  IntPtr proc=OpenProcess(0x440,false,pid); if(proc==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Cannot access a launched client");
  IntPtr b=IntPtr.Zero; try {
   int size=65536,n,status; do {if(b!=IntPtr.Zero) Marshal.FreeHGlobal(b); b=Marshal.AllocHGlobal(size); status=NtQueryInformationProcess(proc,51,b,size,out n); if(status>=0) break; if(status!=unchecked((int)0xC0000004) && status!=unchecked((int)0xC0000023)) throw new Exception("Windows could not inspect the client handles ("+status.ToString("X")+")."); size=Math.Max(size*2,n); if(size>16777216) throw new Exception("Client handle list exceeded the supported size.");} while(true);
   long count=Marshal.ReadInt64(b); if(count<0 || count>(size-16)/40) throw new Exception("Unexpected Windows handle layout."); int closed=0;
   for(int i=0;i<count;i++) { IntPtr value=Marshal.ReadIntPtr(b,16+i*40),copy; if(!DuplicateHandle(proc,value,GetCurrentProcess(),out copy,0,false,2)) continue;
    try {string type=ObjectText(copy,2); if(type!="Mutant"&&type!="Event") continue; string name=ObjectText(copy,1); if(name==null) continue; string leaf=name.Substring(name.LastIndexOf('\\')+1); if(leaf!=prefix+"Mutex"&&leaf!=prefix+"Event") continue;
     IntPtr removed; if(DuplicateHandle(proc,value,GetCurrentProcess(),out removed,0,false,3)) {CloseHandle(removed); closed++;}
    } finally {CloseHandle(copy);}
   } return closed;
  } finally {if(b!=IntPtr.Zero) Marshal.FreeHGlobal(b); CloseHandle(proc);}
 }
 public static void Junction(string link,string target) {
  target=Path.GetFullPath(target).TrimEnd('\\'); if(!Directory.Exists(target) || target.StartsWith(@"\\")) throw new Exception("Select a Roblox installation on a local drive.");
  if(Directory.Exists(link)||File.Exists(link)) throw new Exception("The new launch folder already exists.");
  Directory.CreateDirectory(link); IntPtr h=CreateFile(link,0x40000000,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero);
  if(h==new IntPtr(-1)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  try {byte[] sub=Encoding.Unicode.GetBytes(@"\??\"+target),print=Encoding.Unicode.GetBytes(target); using(var ms=new MemoryStream()) using(var w=new BinaryWriter(ms)) {w.Write(0xA0000003u); w.Write((ushort)(12+sub.Length+print.Length)); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)sub.Length); w.Write((ushort)(sub.Length+2)); w.Write((ushort)print.Length); w.Write(sub); w.Write((ushort)0); w.Write(print); w.Write((ushort)0); byte[] data=ms.ToArray(); int r; if(!DeviceIoControl(h,0x900A4,data,data.Length,IntPtr.Zero,0,out r,IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}}
  finally {CloseHandle(h);}
 }
}
sealed class Client { public Process Process; public string Folder; public string Error; }
sealed class MainForm:Form {
 readonly Label state=new Label(),count=new Label(); readonly ListBox activity=new ListBox(); readonly Button enable=new Button(); readonly List<Client> clients=new List<Client>();
 readonly Protocol protocol=new Protocol(@"Software\Classes\roblox-player",Path.Combine(Disk.Root,"protocol-backup.xml"));
 Settings settings; bool active,working,closing; readonly Queue<string> pending=new Queue<string>(); readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
 public MainForm(bool preview) {
  Text="Roblox Multi Desk"; ClientSize=new Size(620,500); FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(17,23,35); ForeColor=Color.FromArgb(230,236,245); Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
  Label title=LabelAt("MULTI DESK",28,22,564,38); title.Font=new Font("Segoe UI",23,FontStyle.Bold);
  LabelAt("Multiple Roblox windows. One simple launcher.",30,68,560,28);
  LabelAt("MULTI-INSTANCE",30,119,560,25).ForeColor=Color.FromArgb(104,215,190);
  state.SetBounds(30,151,560,55); state.Text="Multi-instance is off. Close existing Roblox game windows,\nthen enable it below."; Controls.Add(state);
  enable.Text="Enable multi-instance"; enable.SetBounds(30,221,560,48); Style(enable); enable.BackColor=Color.FromArgb(27,94,83); enable.Click+=(s,e)=>Try(Toggle); Controls.Add(enable);
  ButtonAt("Choose Roblox player",30,285,273,39,ChoosePlayer);
  ButtonAt("Focus latest game",317,285,273,39,FocusLatest);
  LabelAt("Use your browser to sign in and press Play on Roblox.\nLaunch each different account after the previous game loads.",30,342,560,52);
  count.SetBounds(30,403,560,25); count.Text="0 game processes running"; Controls.Add(count);
  activity.SetBounds(30,435,560,42); activity.BackColor=BackColor; activity.ForeColor=Color.FromArgb(163,180,201); activity.BorderStyle=BorderStyle.None; Controls.Add(activity);
  if(preview) { settings=new Settings(); return; }
  settings=Disk.Read<Settings>(Path.Combine(Disk.Root,"settings.xml"));
  Shown+=(s,e)=>{Try(()=>{if(protocol.Pending){protocol.Restore();Log("Recovered the previous Roblox link handler.");}});StartPipe();};
  timer.Interval=700; timer.Tick+=(s,e)=>RefreshClients(); timer.Start();
  FormClosing+=(s,e)=>{closing=true; timer.Stop(); try {protocol.Restore();} catch(Exception ex) {MessageBox.Show(ex.Message,"Handler recovery needed",MessageBoxButtons.OK,MessageBoxIcon.Warning);} };
 }
 Label LabelAt(string text,int x,int y,int w,int h) { var l=new Label{Text=text}; l.SetBounds(x,y,w,h); Controls.Add(l);return l; }
 void Style(Button b) {b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(67,89,113);b.BackColor=Color.FromArgb(32,49,65);b.ForeColor=ForeColor;}
 void ButtonAt(string text,int x,int y,int w,int h,Action action) {var b=new Button{Text=text};b.SetBounds(x,y,w,h);Style(b);b.Click+=(s,e)=>Try(action);Controls.Add(b);}
 void Try(Action a) {try{a();}catch(Exception ex){MessageBox.Show(ex.Message,"Multi Desk",MessageBoxButtons.OK,MessageBoxIcon.Information);Log(ex.Message.Split('\n')[0]);}}
 void Log(string text) {activity.Items.Insert(0,DateTime.Now.ToString("HH:mm")+"  "+text);while(activity.Items.Count>50)activity.Items.RemoveAt(50);}
 void Save(){Disk.Write(Path.Combine(Disk.Root,"settings.xml"),settings);}
 void ChoosePlayer(){using(var d=new OpenFileDialog{Title="Select RobloxPlayerBeta.exe",Filter="Roblox Player|RobloxPlayerBeta.exe",InitialDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Roblox","Versions")})if(d.ShowDialog()==DialogResult.OK){settings.Player=d.FileName;Save();Log("Roblox player selected.");}}
 string Player(){if(!string.IsNullOrEmpty(settings.Player)&&File.Exists(settings.Player)&&Path.GetFileName(settings.Player).Equals("RobloxPlayerBeta.exe",StringComparison.OrdinalIgnoreCase))return settings.Player;string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Roblox","Versions");if(Directory.Exists(root)){var found=Directory.GetDirectories(root).Select(p=>Path.Combine(p,"RobloxPlayerBeta.exe")).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();if(found!=null)return found;}throw new Exception("Roblox Player was not found. Install Roblox from roblox.com, then use Choose Roblox player.");}
 void Toggle(){if(active){protocol.Restore();active=false;enable.Text="Enable multi-instance";state.Text="Multi-instance is off. Roblox links are restored.";return;}if(Process.GetProcessesByName("RobloxPlayerBeta").Any(p=>{using(p)return !p.HasExited;}))throw new Exception("Close existing Roblox game windows first, then enable multi-instance. Multi Desk leaves those games untouched.");Player();protocol.Enable(Application.ExecutablePath);active=true;enable.Text="Disable multi-instance";state.Text="Multi-instance is on. Keep Multi Desk open.\nSign in using your browser and press Play on Roblox.";Log("Roblox Play links will use Multi Desk until disabled.");}
 void FocusLatest(){var c=clients.LastOrDefault(x=>!x.Process.HasExited);if(c==null)throw new Exception("No game launched by Multi Desk is running.");c.Process.Refresh();IntPtr h=c.Process.MainWindowHandle;if(h==IntPtr.Zero)throw new Exception("The game window is still loading.");Native.ShowWindow(h,9);Native.SetForegroundWindow(h);}
 public void Launch(string url){Try(()=>{if(!active)throw new Exception("Enable multi-instance in Multi Desk, then press Play again.");if(!Links.Launch(url))throw new Exception("Rejected an invalid Roblox launch link.");if(working){if(pending.Count>=6)throw new Exception("Several launches are already queued. Wait for them to open.");pending.Enqueue(url);return;}foreach(var c in clients.Where(c=>!c.Process.HasExited))Native.ClearLocks(c.Process.Id,"ROBLOX_singleton");string player=Player();string folder=Path.Combine(Disk.Root,"Clients",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.GetDirectoryName(folder));Native.Junction(folder,Path.GetDirectoryName(player));var process=Process.Start(new ProcessStartInfo(Path.Combine(folder,"RobloxPlayerBeta.exe"),Links.Quote(url)){UseShellExecute=false,WorkingDirectory=folder});clients.Add(new Client{Process=process,Folder=folder});Log("Game launched. Waiting for the client window.");});}
 void RefreshClients(){if(working||closing)return;if(!active)pending.Clear();else if(pending.Count>0)Launch(pending.Dequeue());foreach(var c in clients.Where(x=>x.Process.HasExited).ToArray()){clients.Remove(c);c.Process.Dispose();try{Directory.Delete(c.Folder,false);}catch{}Log("A launched game has closed.");}count.Text=clients.Count+" game process"+(clients.Count==1?"":"es")+" running";if(!active||clients.Count==0)return;working=true;var snapshot=clients.ToArray();System.Threading.Tasks.Task.Run(()=>{var errors=new List<string>();foreach(var c in snapshot)try{if(!c.Process.HasExited)Native.ClearLocks(c.Process.Id,"ROBLOX_singleton");}catch(Exception ex){if(c.Error!=ex.Message){c.Error=ex.Message;errors.Add(ex.Message);}}if(!closing&&!IsDisposed)try{BeginInvoke((Action)(()=>{working=false;foreach(string error in errors)Log(error);}));}catch(InvalidOperationException){} });}
 void StartPipe(){var thread=new Thread(()=>{while(!closing){try{var security=new PipeSecurity();security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));using(var pipe=new NamedPipeServerStream(Program.Pipe,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.None,4096,4096,security)){pipe.WaitForConnection();using(var reader=new BinaryReader(pipe,Encoding.UTF8)){int len=reader.ReadInt32();if(len<1||len>65536)continue;byte[] data=reader.ReadBytes(len);if(data.Length!=len)continue;string url=Encoding.UTF8.GetString(data);if(Links.Launch(url)&&!closing)BeginInvoke((Action)(()=>Launch(url)));}}}catch{if(!closing)Thread.Sleep(500);}}});thread.IsBackground=true;thread.Start();}
}
static class Program {
 public static string Pipe="MultiDesk-"+WindowsIdentity.GetCurrent().User.Value;
 [STAThread] static int Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try {
   if(args.Length>0&&args[0]=="--test-child"){using(var m=new Mutex(false,args[1]+"Mutex"))using(var e=new EventWaitHandle(false,EventResetMode.AutoReset,args[1]+"Event"))using(var keep=new EventWaitHandle(false,EventResetMode.AutoReset,args[1]+"Keep")){File.WriteAllText(args[2],"ready");Thread.Sleep(15000);}return 0;}
   if(args.Length>0&&args[0]=="--self-test")return Tests.Run(args[1]);
   if(args.Length>0&&args[0]=="--protocol-test")return Tests.ProtocolTest(args[1]);
   if(args.Length==2&&args[0]=="--relay"){if(!Links.Launch(args[1]))return 2;try{using(var pipe=new NamedPipeClientStream(".",Pipe,PipeDirection.Out)){pipe.Connect(4000);using(var w=new BinaryWriter(pipe,Encoding.UTF8)){byte[] b=Encoding.UTF8.GetBytes(args[1]);w.Write(b.Length);w.Write(b);}}}catch{MessageBox.Show("Open Multi Desk and enable multi-instance, then press Play again. If you stopped using Multi Desk, reopen it once to restore Roblox's normal links.","Multi Desk");}return 0;}
   bool created;using(var single=new Mutex(true,"Local\\"+Pipe,out created)){if(!created){MessageBox.Show("Multi Desk is already open.");return 0;}Directory.CreateDirectory(Disk.Root);Application.Run(new MainForm(false));}return 0;
  }catch(Exception ex){MessageBox.Show(ex.Message,"Multi Desk could not start");return 1;}
 }
}
static class Tests {
 public static int ProtocolTest(string folder){Directory.CreateDirectory(folder);string root=@"Software\MultiDeskTest_"+Guid.NewGuid().ToString("N"),backup=Path.Combine(folder,"test-backup.xml");var p=new Protocol(root,backup);var results=new List<string>();try{
  p.Enable(Application.ExecutablePath);Assert(p.Pending,"Backup saved");p.Restore();using(var k=Registry.CurrentUser.OpenSubKey(root))Assert(k==null,"New handler fully removed");Assert(!p.Pending,"Backup removed");results.Add("PASS: new protocol registration and complete restoration.");
  using(var k=Registry.CurrentUser.CreateSubKey(root)){k.SetValue("","Original");k.SetValue("Untouched","preserve");k.SetValue("URL Protocol","old");}using(var k=Registry.CurrentUser.CreateSubKey(root+@"\shell\open\command"))k.SetValue("","original.exe %1",RegistryValueKind.ExpandString);
  p.Enable(Application.ExecutablePath);p.Restore();using(var k=Registry.CurrentUser.OpenSubKey(root)){Assert((string)k.GetValue("")=="Original","Original default");Assert((string)k.GetValue("Untouched")=="preserve","Unrelated value");Assert((string)k.GetValue("URL Protocol")=="old","Original URL marker");}using(var k=Registry.CurrentUser.OpenSubKey(root+@"\shell\open\command")){Assert((string)k.GetValue("")=="original.exe %1","Original command");Assert(k.GetValueKind("")==RegistryValueKind.ExpandString,"Original value kind");}results.Add("PASS: existing handler values and types restored; unrelated data preserved.");
  p.Enable(Application.ExecutablePath);new Protocol(root,backup).Restore();Assert(!p.Pending,"Crash recovery");results.Add("PASS: recovery from persisted backup using a fresh handler instance.");
  p.Enable(Application.ExecutablePath);using(var k=Registry.CurrentUser.CreateSubKey(root+@"\shell\open\command"))k.SetValue("","updated.exe %1");bool blocked=false;try{p.Restore();}catch{blocked=true;}Assert(blocked&&p.Pending,"Concurrent change retains backup");using(var k=Registry.CurrentUser.OpenSubKey(root+@"\shell\open\command"))Assert((string)k.GetValue("")=="updated.exe %1","Updated handler preserved");results.Add("PASS: a concurrent handler update is preserved.");File.WriteAllLines(Path.Combine(folder,"protocol-results.txt"),results);return 0;
 }catch(Exception ex){results.Add("FAIL or unavailable: "+ex);File.WriteAllLines(Path.Combine(folder,"protocol-results.txt"),results);return 1;}finally{try{if(!root.StartsWith(@"Software\MultiDeskTest_"))throw new Exception("Unexpected test key");Registry.CurrentUser.DeleteSubKeyTree(root,false);}catch{}}}
 static void Assert(bool v,string message){if(!v)throw new Exception(message);}
 public static int Run(string folder){Directory.CreateDirectory(folder);var lines=new List<string>();try{
  Assert(Links.Launch("roblox-player:1+launchmode:play"),"Launch URL");foreach(string bad in new[]{"https://www.roblox.com/","roblox-player:foo\\\" bar","roblox-player:foo\\nbar","roblox-player:"})Assert(!Links.Launch(bad),"Invalid launch rejected");lines.Add("PASS: launch protocol and argument validation.");
  string xml=Path.Combine(folder,"settings.xml");Disk.Write(xml,new Settings{Profiles=new List<Profile>{new Profile{Id=Guid.NewGuid().ToString("N"),Name="Main & second"}}});Assert(Disk.Read<Settings>(xml).Profiles[0].Name=="Main & second","Settings roundtrip");Disk.Write(xml,new Settings());Assert(Disk.Read<Settings>(xml).Profiles.Count==0,"Atomic overwrite");lines.Add("PASS: settings roundtrip and atomic replacement.");
  string target=Path.Combine(folder,"junction-target"),link=Path.Combine(folder,"junction-link");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"proof.txt"),"okay");Native.Junction(link,target);Assert(File.ReadAllText(Path.Combine(link,"proof.txt"))=="okay","Junction resolves");Directory.Delete(link,false);Assert(File.Exists(Path.Combine(target,"proof.txt")),"Target preserved");lines.Add("PASS: unique launch junction, unlink preserves original files.");
  string prefix="MultiDeskTest_"+Guid.NewGuid().ToString("N")+"_",ready=Path.Combine(folder,"ready.txt");using(var child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--test-child "+Links.Quote(prefix)+" "+Links.Quote(ready)){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){try{var sw=Stopwatch.StartNew();while(!File.Exists(ready)&&sw.ElapsedMilliseconds<6000)Thread.Sleep(50);Assert(File.Exists(ready),"Test child ready");int closed=Native.ClearLocks(child.Id,prefix);Assert(closed==2,"Exactly two matching handles removed, got "+closed);using(var keep=EventWaitHandle.OpenExisting(prefix+"Keep"))Assert(keep!=null,"Unrelated event preserved");Assert(Native.ClearLocks(child.Id,prefix)==0,"Repeated scan harmless");lines.Add("PASS: synthetic child lock removal, unrelated handle preserved, repeat scan.");}finally{if(!child.HasExited)child.Kill();child.WaitForExit();}}
  using(var form=new MainForm(true)){form.ShowInTaskbar=false;form.Opacity=0;form.Show();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,"preview.png"));}form.Close();}lines.Add("PASS: desktop form construction and render.");
  File.WriteAllLines(Path.Combine(folder,"test-results.txt"),lines);return 0;
 }catch(Exception ex){lines.Add("FAIL: "+ex);File.WriteAllLines(Path.Combine(folder,"test-results.txt"),lines);return 1;}}
}
}
