using System.IO;
namespace TinyScholar.Switcher;

// Text and paragraph order transcribed from the v0.84 HTA; <strong> is
// retained as formatting metadata for the WPF renderer, never shown literally.
public static class HtaMessages
{
    public const string BackupNote="A temporary safety backup is made before changes. It is deleted after a successful switch and kept if an error occurs.";
    public const string SourceKept="Your stored source pack(s) mods folder(s) will not be deleted.";
    public const string Progress="Preparing, checking, and applying your loadout… please keep this window open.";
    public const string Cancelled="No changes were made to your active Mods folders. Your current loadout remains as it was.";
    public const string Empty="No managed pack folders currently active.";
    public static LoadoutPrompt SourceMissing(string path)=>new("Source folder not found",
        "The folder containing your expansion and Store-world folders could not be found:\n\n"+path+"\n\nPlease check the STORAGE path. It must point directly to the folder containing folders such as EP01_WorldAdventures and DT01_Riverview.\n\nNothing has been changed.",[],"OK");
    public static LoadoutPrompt ModsMissing(string path)=>new("Active Mods path problem",
        "The active Mods folder, Packages folder, or Overrides folder could not be found.\n\nCurrent Mods path:\n"+path+"\n\nPlease correct the path above and make sure Packages and Overrides exist. Nothing has been changed.",[],"OK");
    public static LoadoutPrompt Missing(string[] codes)=>new("Some selected pack folders ARE MISSING",
        "These selected packs could not be found or have neither a Packages nor an Overrides folder:",codes,"Continue with available packs",AfterText:
        "If a pack should be there, check the STORAGE path. It must point directly to the folder containing folders such as EP01_WorldAdventures and DT01_Riverview.\n\nContinue with available packs and skip missing ones?");
    public static LoadoutPrompt Duplicates(IReadOnlyList<LoadoutEngine.Duplicate> duplicates)=>new("Duplicate filenames found",
        "These filenames appear more than once. Each filename is followed by the packs and file locations that contain it. Click a path to show that file in File Explorer:",[],"Continue",duplicates,
        "These files will not be deleted or overwritten because of this warning. Duplicate filenames can sometimes indicate conflicting mods and may cause unexpected problems in the game. This checks filenames, not file contents, so some duplicates may be harmless. Continue anyway?");
    public static LoadoutPrompt Removal(string[] codes)=>new("Active packs NOT SELECTED",
        "These active packs are not selected for your next loadout:",codes,"Continue",AfterText:
        "Their active Mods folders <strong>WILL BE REMOVED</strong> when the new loadout is applied.\n\n"+SourceKept+"\n\n"+BackupNote+"\n\nContinue?");
    public static LoadoutPrompt Final(string[] chosen,int replacing)
    {
        string after=chosen.Length==0?"Existing base-game mods and unrelated folders will not be touched.":
            (replacing>0?replacing+" selected pack(s) already have active Mods folders. Their active copies <strong>WILL BE REPLACED</strong> with the stored versions.\n\n":"")+
            "Base-game mods and unrelated folders will not be touched.";
        return new(chosen.Length==0?"Base Game ONLY":replacing>0?"Selected active pack folders WILL BE REPLACED":"Ready to apply",
            chosen.Length==0?"No expansion packs, stuff packs, or Store worlds are selected. After applying, only the Base Game mods folder will remain active among the folders managed by this switcher.":"The following "+chosen.Length+" pack(s) will be applied:",
            chosen,replacing>0?"Back up and apply":"Apply loadout",AfterText:after+"\n\n"+SourceKept+"\n\n"+BackupNote,ShowBaseGame:chosen.Length==0);
    }
    public static LoadoutPrompt Complete(string[] chosen,string cleanup)=>new("Loadout complete",
        "✧ Your loadout is complete. Have fun playing The Sims 3! ✧\n\n"+
        (chosen.Length==0?"No additional pack mods folders were applied. Base Game only remains selected:":chosen.Length==1?"The following pack mods folder was successfully applied:":"The following "+chosen.Length+" pack mods folders were successfully applied:"),
        chosen,"OK",AfterText:cleanup,ShowBaseGame:chosen.Length==0);
}

public sealed record RefreshFeedback(string Text,bool Error);
public sealed class RefreshDisplay
{
    public string[] ActiveCodes {get;private set;}=[];
    public bool HasSnapshot {get;private set;}
    public RefreshFeedback? Feedback {get;private set;}
    public bool Update(LoadoutEngine.Inspection state,bool showFeedback,DateTime now)
    {
        if(state.Error!=null)
        {
            if(showFeedback)Feedback=new("Refresh unsuccessful — "+state.Error,true);
            return false;
        }
        if(state.ModsReady){ActiveCodes=state.ActiveCodes.ToArray();HasSnapshot=true;}
        if(showFeedback)
            Feedback=state.PathProblems.Length>0
                ?new("Refresh incomplete — "+string.Join(" ",state.PathProblems)+(state.ModsReady?" Active status updated.":" No active packs could be checked."),true)
                :new("Refresh successful ("+now.ToString("HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture)+")",false);
        return state.ModsReady;
    }
}
