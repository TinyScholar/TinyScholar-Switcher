using System.IO;
namespace TinyScholar.Switcher;

public sealed record LoadoutPrompt(string Title, string Text, string[] Codes, string Proceed,
    IReadOnlyList<LoadoutEngine.Duplicate>? Duplicates = null, string AfterText = "", bool ShowBaseGame = false);
public sealed record WorkflowResult(bool Cancelled, LoadoutEngine.ApplyResult? Applied);

// Confirmations are independent of the WPF view, so cancelling each step can
// be exercised against real disposable folders without driving a Windows UI.
public sealed class LoadoutWorkflow(LoadoutEngine engine)
{
    public async Task<WorkflowResult> Run(string storage, string mods, string[] selected,
        Func<LoadoutPrompt,Task<bool>> confirm, Action<string[]> selectionChanged, Action<string> progress)
    {

        var chosen=selected.ToArray();
        var plan=await Task.Run(()=>engine.Prepare(storage,mods,chosen));
        if(plan.MissingCodes.Length>0)
        {
            if(!await confirm(HtaMessages.Missing(plan.MissingCodes)))return new(true,null);
            chosen=chosen.Except(plan.MissingCodes).ToArray();
            selectionChanged(chosen);
            plan=await Task.Run(()=>engine.Prepare(storage,mods,chosen));
        }
        if(plan.Duplicates.Count>0 && !await confirm(HtaMessages.Duplicates(plan.Duplicates)))return new(true,null);
        if(plan.RemovedCodes.Length>0 && !await confirm(HtaMessages.Removal(plan.RemovedCodes)))return new(true,null);
        if(!await confirm(HtaMessages.Final(chosen,plan.ReplacingCodes.Length)))return new(true,null);
        var current=await Task.Run(()=>engine.Prepare(storage,mods,chosen));
        if(!Equivalent(plan,current))throw new IOException("The pack folders changed while the confirmation was open. Please review and apply again. No changes were made by the Switcher.");
        progress(HtaMessages.Progress);
        var result=await Task.Run(()=>engine.Apply(storage,mods,chosen));
        return new(false,result);
    }
    private static bool Equivalent(LoadoutEngine.Analysis a,LoadoutEngine.Analysis b)
    {
        return a.MissingCodes.SequenceEqual(b.MissingCodes) && a.RemovedCodes.SequenceEqual(b.RemovedCodes)
            && a.ReplacingCodes.SequenceEqual(b.ReplacingCodes) && a.Duplicates.Count==b.Duplicates.Count
            && a.Duplicates.Zip(b.Duplicates,(x,y)=>x.Name==y.Name && x.Occurrences.SequenceEqual(y.Occurrences)).All(equal=>equal);
    }
}
