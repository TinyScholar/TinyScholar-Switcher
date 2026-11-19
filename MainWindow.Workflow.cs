using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TinyScholar.Switcher;
public partial class MainWindow
{
    private bool busy;
    private TaskCompletionSource<bool>? dialogCompletion;
    private void CardClicked(object sender,MouseButtonEventArgs e)
    {
        if(busy)return;
        // Checkbox consumes its own mouse event. Other card surfaces bubble here.
        var border=(Border)sender;
        var pick=(CheckBox)FindName("Pick"+border.Name.Substring(4));
        pick.Focus();pick.IsChecked=pick.IsChecked!=true;e.Handled=true;
    }
    private async void ApplyLoadout(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        CommitPaths();
        var storage=prefs.Storage.Trim();var mods=prefs.Mods.Trim();
        var chosen=cards.Where(c=>c.IsChecked==true).Select(c=>(string)c.Tag).ToArray();
        busy=true;CanvasRoot.IsEnabled=false;
        try
        {
            if(!Directory.Exists(storage)){await ShowPrompt(HtaMessages.SourceMissing(storage),false);return;}
            if(!Directory.Exists(mods)||!Directory.Exists(Path.Combine(mods,"Packages"))||!Directory.Exists(Path.Combine(mods,"Overrides")))
            {await ShowPrompt(HtaMessages.ModsMissing(mods),false);return;}
            var workflow=new LoadoutWorkflow(engine);
            var outcome=await workflow.Run(storage,mods,chosen,
                prompt=>ShowPrompt(prompt),
                available=>{foreach(var card in cards)card.IsChecked=available.Contains((string)card.Tag);},
                message=>{ProgressText.Text=message;ProgressOverlay.Visibility=Visibility.Visible;});
            ProgressOverlay.Visibility=Visibility.Collapsed;
            if(outcome.Cancelled)
                await ShowPrompt(new("No changes made",HtaMessages.Cancelled,Array.Empty<string>(),"OK"),false);
            else if(outcome.Applied is { } result)
            {
                Refresh();
                if(result.Success)
                {
                    var applied=cards.Where(c=>c.IsChecked==true).Select(c=>(string)c.Tag).ToArray();
                    foreach(var card in cards)card.IsChecked=false;
                    await ShowPrompt(HtaMessages.Complete(applied,result.Message),false,result.BackupPath);
                }
                else await ShowPrompt(new("Apply stopped",result.Message,Array.Empty<string>(),"OK"),false,result.BackupPath);
            }
        }
        catch(Exception ex)
        {
            ProgressOverlay.Visibility=Visibility.Collapsed;
            await ShowPrompt(new("Apply stopped",ex.Message,Array.Empty<string>(),"OK"),false);
        }
        finally
        {
            busy=false;CanvasRoot.IsEnabled=true;ProgressOverlay.Visibility=Visibility.Collapsed;
            ApplyPreview.Focus();
        }
    }
    private TextBlock DialogText(string text, double size=16)
    {
        var block=new TextBlock { FontFamily=new FontFamily("Trebuchet MS"),FontSize=size,TextWrapping=TextWrapping.Wrap,
            LineHeight=size*1.55,Margin=new Thickness(0,0,0,11) };
        block.SetResourceReference(TextBlock.ForegroundProperty,"DialogInk");
        foreach(var part in System.Text.RegularExpressions.Regex.Split(text,"(<strong>.*?</strong>)"))
            block.Inlines.Add(part.StartsWith("<strong>")?new Bold(new Run(part.Substring(8,part.Length-17))):new Run(part));
        return block;
    }
    private void AddPack(string code,Panel? parent=null,bool duplicate=false,bool removal=false)
    {
        Panel row=removal ? new StackPanel { Margin=new Thickness(0,7,0,7) }
            : new DockPanel { Margin=new Thickness(0,7,0,duplicate?2:7) };
        string id=code.Split('_')[0];
        var icon=new Image { Width=36,Height=36,Margin=new Thickness(0,0,12,0),
            Source=new BitmapImage(new Uri("pack://application:,,,/Assets/"+id+".png")) };
        if(removal) { icon.HorizontalAlignment=HorizontalAlignment.Left;icon.Margin=new Thickness(0,0,12,7); }
        else DockPanel.SetDock(icon,Dock.Left);
        row.Children.Add(icon);
        var text=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
        var pick=FindName("Pick"+id) as CheckBox;
        var title=DialogText(((string?)pick?.ToolTip??"Base Game").ToUpperInvariant(),14);
        title.FontWeight=FontWeights.Bold;title.LineHeight=21.7;title.Margin=new Thickness(0,0,0,3);text.Children.Add(title);
        var folder=DialogText(code,12);folder.FontFamily=new FontFamily("Consolas");folder.LineHeight=18.6;folder.Margin=new Thickness(0);text.Children.Add(folder);
        title.SetResourceReference(TextBlock.ForegroundProperty,"ConfirmName");
        folder.SetResourceReference(TextBlock.ForegroundProperty,"ConfirmCode");
        row.Children.Add(text);
        var border=new Border {Child=row,BorderThickness=new Thickness(0,0,0,duplicate?0:1),Padding=new Thickness(3,0,3,0),MinHeight=duplicate?49:65};
        border.SetResourceReference(Border.BorderBrushProperty,"ConfirmRowLine");(parent??DialogBody).Children.Add(border);
    }
    private void AddPath(string path,bool file,Panel? parent=null)
    {
        var block=DialogText("",13);block.FontFamily=new FontFamily("Consolas");block.LineHeight=19.5;block.Margin=new Thickness(0);
        var link=new Hyperlink(new Run(path)) { ToolTip=file?"Show this file in File Explorer":"Open backup folder in File Explorer" };link.SetResourceReference(Hyperlink.ForegroundProperty,"Accent");
        link.Click+=(_,_)=>{
            try
            {
                if(file?!File.Exists(path):!Directory.Exists(path))
                {MessageBox.Show(file?"The duplicate file could not be found at this location.":"The backup folder could not be found at this location.");return;}
                var start=new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute=true };
                if(file)start.Arguments="/select,\""+path.Replace("\"","")+"\"";
                else start.Arguments="\""+path.Replace("\"", "")+"\"";
                System.Diagnostics.Process.Start(start);
            }
            catch(Exception ex){MessageBox.Show("Could not open File Explorer: "+ex.Message);}
        };
        block.Inlines.Add(link);
        var box=new Border {Child=block,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,2,0,10),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5)};
        box.SetResourceReference(Border.BorderBrushProperty,"DuplicatePathLine");
        box.SetResourceReference(Border.BackgroundProperty,"DuplicatePathFill");(parent??DialogBody).Children.Add(box);
    }
    private Task<bool> ShowPrompt(LoadoutPrompt prompt,bool cancellable=true,string? backup=null)
    {
        ProgressOverlay.Visibility=Visibility.Collapsed;
        DialogTitle.Inlines.Clear();
        string strong=prompt.Title switch {"Active packs NOT SELECTED"=>"NOT SELECTED","Selected active pack folders WILL BE REPLACED"=>"WILL BE REPLACED","Base Game ONLY"=>"ONLY","Some selected pack folders ARE MISSING"=>"ARE MISSING",_=>""};
        if(strong.Length==0)DialogTitle.Inlines.Add(prompt.Title);
        else {DialogTitle.Inlines.Add(prompt.Title.Substring(0,prompt.Title.Length-strong.Length));DialogTitle.Inlines.Add(new Bold(new Run(strong)));}
        DialogFrame.Width=prompt.Duplicates!=null?800:prompt.Title=="Apply stopped"?760:590;
        DialogBody.Children.Clear();
        foreach(string paragraph in prompt.Text.Split(new[]{"\n\n"},StringSplitOptions.None))DialogBody.Children.Add(DialogText(paragraph));
        if(prompt.Codes.Length>0||prompt.ShowBaseGame)
        {
            var list=new StackPanel();
            foreach(string code in prompt.Codes)AddPack(code,list,removal:prompt.Title=="Active packs NOT SELECTED");
            if(prompt.ShowBaseGame)AddPack("EP00_BaseGame",list);
            var scroll=new ScrollViewer {Content=list,MaxHeight=prompt.Title=="Active packs NOT SELECTED"?203:208,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var frame=new Border {Child=scroll,BorderThickness=new Thickness(0,1,0,1),Margin=new Thickness(0,1,0,12)};
            frame.SetResourceReference(Border.BorderBrushProperty,"ConfirmListLine");DialogBody.Children.Add(frame);
        }
        if(prompt.Duplicates!=null)
        {
            var list=new StackPanel();
            foreach(var duplicate in prompt.Duplicates.Take(80))
            {
                var label=DialogText(duplicate.Name.ToLowerInvariant(),12);label.FontFamily=new FontFamily("Consolas");label.Margin=new Thickness(0,12,0,5);label.FontWeight=FontWeights.Bold;list.Children.Add(label);
                foreach(var match in duplicate.Occurrences){AddPack(match.Code,list,true);AddPath(match.Path,true,list);}
            }
            if(prompt.Duplicates.Count>80)list.Children.Add(DialogText("…and "+(prompt.Duplicates.Count-80)+" more filenames",11));
            var frame=new Border {Child=new ScrollViewer {Content=list,Padding=new Thickness(9),MaxHeight=388,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},Padding=new Thickness(0),Margin=new Thickness(0,0,0,10),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4)};
            frame.SetResourceReference(Border.BorderBrushProperty,"ConfirmListLine");DialogBody.Children.Add(frame);
        }
        foreach(string paragraph in prompt.AfterText.Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries))
        {
            var block=DialogText(paragraph);
            if(paragraph==HtaMessages.BackupNote||paragraph=="The temporary backup has been removed after the successful switch.")block.SetResourceReference(TextBlock.ForegroundProperty,"ConfirmNote");
            DialogBody.Children.Add(block);
        }
        if(backup!=null)
        {
            DialogBody.Children.Add(DialogText("A backup was created here (click to open in File Explorer):"));AddPath(backup,false);
            DialogBody.Children.Add(DialogText("Please do not delete it until you have checked your Mods folders."));
        }
        if(DialogBody.Children.Count>0 && DialogBody.Children[DialogBody.Children.Count-1] is FrameworkElement last)
            last.Margin=new Thickness(last.Margin.Left,last.Margin.Top,last.Margin.Right,0);
        AdaptLayout();
        DialogAccept.Content=new TextBlock {Text=prompt.Proceed.ToUpperInvariant(),FontSize=14,FontWeight=FontWeights.Normal};
        DialogCancel.Content=new TextBlock {Text="CANCEL",FontSize=14,FontWeight=FontWeights.Normal};DialogCancel.Visibility=cancellable?Visibility.Visible:Visibility.Collapsed;
        dialogCompletion=new(TaskCreationOptions.RunContinuationsAsynchronously);
        DialogOverlay.Visibility=Visibility.Visible;DialogScroll.ScrollToTop();
        if(cancellable)DialogCancel.Focus();else DialogAccept.Focus();
        return dialogCompletion.Task;
    }
    private void FinishDialog(bool accepted)
    {
        DialogOverlay.Visibility=Visibility.Collapsed;
        var pending=dialogCompletion;dialogCompletion=null;pending?.TrySetResult(accepted);
    }
    private void AcceptDialog(object sender,RoutedEventArgs e)=>FinishDialog(true);
    private void CancelDialog(object sender,RoutedEventArgs e)=>FinishDialog(false);
    private void DialogKeyDown(object sender,KeyEventArgs e)
    {
        if(dialogCompletion!=null && e.Key==Key.Escape){FinishDialog(DialogCancel.Visibility!=Visibility.Visible);e.Handled=true;}
    }
    private void PreventCloseDuringApply(object? sender,CancelEventArgs e)
    {
        if(!busy)return;
        e.Cancel=true;
        if(dialogCompletion!=null)FinishDialog(false);

    }
}
