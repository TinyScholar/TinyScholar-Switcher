using TinyScholar.Switcher;
using System.Security.Cryptography;
const string A="EP01_WorldAdventures",B="EP02_Ambitions",C="EP03_LateNight";
var root=Path.Combine(Path.GetTempPath(),"TinyScholar_tests_"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);int passed=0;
void Check(bool yes,string message){if(!yes)throw new Exception(message);}
void Write(string path,string value){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,value);}
string Snapshot(string path)=>string.Join("\n",Directory.GetFiles(path,"*",SearchOption.AllDirectories).Order().Select(f=>Path.GetRelativePath(path,f)+":"+Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f)))));
(string storage,string mods) Setup(string test)
{
 var parent=Path.Combine(root,test);var storage=Path.Combine(parent,"STORAGE");var mods=Path.Combine(parent,"Mods");Directory.CreateDirectory(storage);
 Directory.CreateDirectory(Path.Combine(mods,"Packages"));Directory.CreateDirectory(Path.Combine(mods,"Overrides"));
 Write(Path.Combine(storage,A,"Packages","nested","a.package"),"source A");Write(Path.Combine(storage,A,"Overrides","a_override.package"),"override A");
 Write(Path.Combine(storage,B,"Packages","b.package"),"source B");
 Write(Path.Combine(mods,"Packages","EP00_BaseGame","keep.package"),"base untouched");Write(Path.Combine(mods,"Packages","unrelated","keep.txt"),"unrelated untouched");Write(Path.Combine(mods,"Resource.cfg"),"config untouched");return(storage,mods);
}
var engine=new LoadoutEngine();
async Task Test(string name,Func<Task> run){await run();Console.WriteLine("PASS "+name);passed++;}
try
{
 await Test("copy packages, overrides, nested files; keep source/base/unrelated",async()=>{
 var(s,m)=Setup("copy");string src=Snapshot(s);var w=new LoadoutWorkflow(engine);var result=await w.Run(s,m,[A],_=>Task.FromResult(true),_=>{},_=>{});
 Check(result.Applied?.Success==true,"copy failed");Check(File.ReadAllText(Path.Combine(m,"Packages",A,"nested","a.package"))=="source A","nested file");Check(File.Exists(Path.Combine(m,"Overrides",A,"a_override.package")),"override missing");Check(src==Snapshot(s),"source changed");Check(File.ReadAllText(Path.Combine(m,"Packages","EP00_BaseGame","keep.package"))=="base untouched","base changed");Check(File.Exists(Path.Combine(m,"Packages","unrelated","keep.txt")),"unrelated removed");Check(result.Applied!.BackupPath==null,"backup not cleaned");});
 await Test("replace selected active pack and remove unselected active pack",async()=>{
 var(s,m)=Setup("replace");Write(Path.Combine(m,"Packages",A,"obsolete.txt"),"old");Write(Path.Combine(m,"Overrides",B,"old.txt"),"old B");var prompts=new List<string>();
 var result=await new LoadoutWorkflow(engine).Run(s,m,[A],p=>{prompts.Add(p.Title);return Task.FromResult(true);},_=>{},_=>{});
 Check(result.Applied?.Success==true,"replace failed");Check(!File.Exists(Path.Combine(m,"Packages",A,"obsolete.txt")),"obsolete survived");Check(!Directory.Exists(Path.Combine(m,"Overrides",B)),"unselected survived");Check(prompts.SequenceEqual(new[]{"Active packs NOT SELECTED","Selected active pack folders WILL BE REPLACED"}),"confirmation order");});
 foreach(string gate in new[]{"missing","duplicates","removal","final"})
 await Test("cancel at "+gate+" leaves all file bytes unchanged",async()=>{
 var(s,m)=Setup("cancel_"+gate);Write(Path.Combine(m,"Packages",B,"old.txt"),"old B");
 if(gate=="duplicates")Write(Path.Combine(s,"EP00_BaseGame","Packages","a.package"),"duplicate");
 var before=Snapshot(m);var source=Snapshot(s);string[] chosen=gate=="missing"?[A,C]:[A];
 bool Cancel(LoadoutPrompt p)=>gate switch{"missing"=>p.Title.Contains("MISSING"),"duplicates"=>p.Title.Contains("Duplicate"),"removal"=>p.Title.Contains("NOT SELECTED"),_=>p.Title=="Ready to apply"};
 var result=await new LoadoutWorkflow(engine).Run(s,m,chosen,p=>Task.FromResult(!Cancel(p)),_=>{},_=>{});
 Check(result.Cancelled,"did not cancel");Check(before==Snapshot(m)&&source==Snapshot(s),"cancel changed files");});
 await Test("skip missing packs updates selection and copies only available",async()=>{
 var(s,m)=Setup("skip");string[]? actual=null;var result=await new LoadoutWorkflow(engine).Run(s,m,[A,C],_=>Task.FromResult(true),a=>actual=a,_=>{});
 Check(result.Applied?.Success==true&&actual!.SequenceEqual(new[]{A}),"missing skip");Check(!Directory.Exists(Path.Combine(m,"Packages",C)),"missing copied");});
 await Test("duplicates include Base Game, packages, overrides and nested paths",()=>{
 var(s,m)=Setup("dupes");Write(Path.Combine(s,"EP00_BaseGame","Packages","a.package"),"base duplicate");Write(Path.Combine(s,B,"Overrides","deep","a.package"),"override duplicate");
 var plan=engine.Prepare(s,m,[A,B]);Check(plan.Duplicates.Single(d=>d.Name=="a.package").Occurrences.Count==3,"duplicate locations");return Task.CompletedTask;});
 await Test("empty selection removes managed packs, preserves base and unrelated",async()=>{
 var(s,m)=Setup("empty");Write(Path.Combine(m,"Packages",A,"old.txt"),"old A");var prompts=new List<string>();var result=await new LoadoutWorkflow(engine).Run(s,m,[],p=>{prompts.Add(p.Title);return Task.FromResult(true);},_=>{},_=>{});
 Check(result.Applied?.Success==true&&!Directory.Exists(Path.Combine(m,"Packages",A)),"empty selection failed");Check(File.Exists(Path.Combine(m,"Packages","EP00_BaseGame","keep.package"))&&File.Exists(Path.Combine(m,"Resource.cfg")),"preservation failed");Check(prompts.Last()=="Base Game ONLY","no base-only confirmation");});
 await Test("failure after deletion restores previous files and retains backup",()=>{
 var(s,m)=Setup("rollback");Write(Path.Combine(m,"Packages",A,"old.txt"),"previous loadout");
 // An ordinary file occupies the destination directory name: staging succeeds,
 // copying after deletion fails, and recovery must restore the old managed pack.
 Write(Path.Combine(m,"Packages",B),"blocking file");var before=Snapshot(m);var result=engine.Apply(s,m,[B]);
 Check(!result.Success,"expected failure");Check(before==Snapshot(m),"rollback not byte-identical");Check(result.BackupPath!=null&&Directory.Exists(result.BackupPath),"backup not retained");Check(result.Message.Contains("The switcher also attempted to restore the previous managed folders."),"restoration status");Directory.Delete(result.BackupPath!,true);return Task.CompletedTask;});
 await Test("invalid roots and unknown pack codes never alter active files",()=>{
 var(s,m)=Setup("invalid");var before=Snapshot(m);
 Check(!engine.Apply(s,m,["..\\evil"]).Success,"unknown code accepted");Check(!engine.Apply(m,m,[A]).Success,"overlap accepted");Check(!engine.Apply(s,m,[A,A]).Success,"duplicate selection accepted");Check(before==Snapshot(m),"invalid request changed files");return Task.CompletedTask;});
 await Test("folder changes during confirmation stop before any apply",async()=>{
 var(s,m)=Setup("changed");var before=Snapshot(m);bool rejected=false;
 try{await new LoadoutWorkflow(engine).Run(s,m,[A],p=>{Directory.Delete(Path.Combine(s,A),true);return Task.FromResult(true);},_=>{},_=>{});}catch(IOException){rejected=true;}
 Check(rejected&&before==Snapshot(m),"changed plan was applied");});
 await Test("source symlink rejection leaves active files untouched",()=>{
 var(s,m)=Setup("link");var before=Snapshot(m);File.CreateSymbolicLink(Path.Combine(s,A,"Packages","link.package"),Path.Combine(s,B,"Packages","b.package"));var result=engine.Apply(s,m,[A]);Check(!result.Success&&before==Snapshot(m),"linked file accepted");return Task.CompletedTask;});
 await Test("HTA refresh success timestamp and failed-refresh snapshot preservation",()=>{
 var view=new RefreshDisplay();var time=new DateTime(2026,11,10,9,2,4);
 Check(view.Update(new(true,[],[A]),true,time),"valid scan rejected");
 Check(view.Feedback==new RefreshFeedback("Refresh successful (09:02:04)",false),"success message");
 Check(!view.Update(new(false,["Active Mods folder is missing Packages or Overrides."],[]),true,time),"invalid scan accepted");
 Check(view.ActiveCodes.SequenceEqual(new[]{A}),"last active snapshot lost");
 Check(view.Feedback==new RefreshFeedback("Refresh incomplete — Active Mods folder is missing Packages or Overrides. No active packs could be checked.",true),"incomplete message");
 var prior=view.Feedback;view.Update(new(true,[],[]),false,time);Check(view.Feedback==prior,"silent refresh changed feedback");
 view.Update(new(true,["STORAGE folder not found."],[B]),true,time);
 Check(view.ActiveCodes.SequenceEqual(new[]{B})&&view.Feedback!.Text=="Refresh incomplete — STORAGE folder not found. Active status updated.","usable mods not updated");
 view.Update(new(false,[],[],"Access denied"),true,time);Check(view.Feedback!.Text=="Refresh unsuccessful — Access denied"&&view.ActiveCodes.SequenceEqual(new[]{B}),"exception message or snapshot");
 return Task.CompletedTask;});
 await Test("HTA confirmation variants, emphasis, and completion grammar",()=>{
 Check(HtaMessages.Final([],0).ShowBaseGame&&HtaMessages.Final([],0).Title=="Base Game ONLY","base game");
 Check(HtaMessages.Final([A],1).Proceed=="Back up and apply"&&HtaMessages.Final([A],1).AfterText.Contains("<strong>WILL BE REPLACED</strong>"),"replacement");
 Check(HtaMessages.Missing([A]).Proceed=="Continue with available packs","missing button");
 Check(HtaMessages.Removal([A]).AfterText.EndsWith("Continue?")&&HtaMessages.Removal([A]).AfterText.Contains("<strong>WILL BE REMOVED</strong>"),"removal");
 Check(HtaMessages.Complete([],"").ShowBaseGame&&HtaMessages.Complete([A],"").Text.Contains("The following pack mods folder was successfully applied:")&&HtaMessages.Complete([A,B],"").Text.Contains("The following 2 pack mods folders were successfully applied:"),"completion grammar");
 return Task.CompletedTask;});
 await Test("portable settings roundtrip and previous-version JSON compatibility",()=>{
 var settings=new Preferences(true,@"C:\Modding\STORAGE é",@"C:\Users\Isa\Documents\Electronic Arts\The Sims 3\Mods");
 Check(SettingsStore.Read(SettingsStore.Write(settings))==settings,"settings roundtrip");
 var previous="{\"Light\":true,\"Storage\":\"source\",\"Mods\":\"mods\"}";
 Check(SettingsStore.Read(previous)==new Preferences(true,"source","mods"),"previous settings not read");
 return Task.CompletedTask;});
 Console.WriteLine($"{passed} integration checks passed.");
}
finally{Directory.Delete(root,true);}
