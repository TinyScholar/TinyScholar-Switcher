using System.IO;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace TinyScholar.Switcher;

public partial class MainWindow : Window
{
    private readonly string settingsFile = Path.Combine(AppContext.BaseDirectory, "settings.json");
    private Preferences prefs = new(false, "", "");
    private readonly LoadoutEngine engine = new();
    private readonly RefreshDisplay refreshDisplay = new();
    private readonly List<CheckBox> cards = new();

    public MainWindow()
    {
        InitializeComponent();
        string warning = "";
        try
        {
            if (File.Exists(settingsFile))
                prefs = SettingsStore.Read(File.ReadAllText(settingsFile)) ?? prefs;
        }
        catch (Exception ex) { warning = "Could not read the saved settings. " + ex.Message; }
        foreach (string code in LoadoutEngine.AllowedCodes)
            cards.Add((CheckBox)FindName("Pick" + code.Split('_')[0]));
        SizeChanged += (_, _) => AdaptLayout();
        Loaded += (_, _) => AdaptLayout();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(new Action(AdaptLayout));
        Closing += PreventCloseDuringApply;
        SourceInitialized += (_, _) => UpdateCaption();
        ApplyTheme();
        Refresh();
        if (warning.Length > 0) { Notice.Text += "\n" + warning; Notice.Visibility=Visibility.Visible; }
        UpdateCount();
    }

    private void ApplyTheme()
    {
        void Brush(string key, string dark, string light) =>
            Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(prefs.Light ? light : dark));
        Brush("Page", "#17131a", "#f7f7f8");
        Brush("PanelFill", "#121016", "#ffffff");
        Brush("PanelInset","#0017131a","#00ffffff");
        Brush("CardInset","#3c2b28","#e6dce9");
        Brush("ActiveInset","#3c2b28","#cfe5d3");
        Brush("Line", "#92653b", "#aa8d6b");
        Brush("Divider", "#65462e", "#d8cbd9");
        Brush("Card", "#211b26", "#f5f1f7");
        Brush("CardActive", "#211b26", "#f0f8f1");
        Brush("CardLine", "#785638", "#b8a4bd");
        Brush("CardActiveLine", "#7b9b68", "#44835a");
        Brush("Brand", "#f5dfb6", "#59376e");
        Brush("Accent", "#e5bd7b", "#59376e");
        Brush("Ink", "#f3e6d0", "#30213a");
        Brush("PackInk", "#f2e2cb", "#30213a");
        Brush("PathLabel", "#d7b995", "#4d3d52");
        Brush("PathFill", "#0e0d11", "#ffffff");
        Brush("PathLine", "#755338", "#97849f");
        Brush("ActiveListInk","#e7d8c7","#514b57");
        Brush("ActiveGem","#80db9a","#277748");
        Brush("DuplicatePathFill","#151017","#f7f2f8");
        Brush("DuplicatePathLine","#735239","#b9a8bf");
        Brush("ConfirmName","#ffdfad","#59376e");
        Brush("ConfirmCode","#c7a88a","#57505e");
        Brush("ConfirmListLine","#654b35","#d6c9db");
        Brush("ConfirmRowLine","#403127","#e5dce8");
        Brush("ConfirmNote","#e2c7a5","#423747");
        Brush("ProgressInk","#cfe4c9","#286c45");
        Brush("DialogShade","#40000000","#52221828");
        Brush("DialogFill","#211923","#ffffff");
        Brush("DialogLine","#d0a268","#95739e");
        Brush("DialogHeading","#e8c58f","#59376e");
        Brush("DialogInk","#f1e3d1","#30213a");
        Brush("FeedbackSuccess","#d7b477","#59376e");
        Brush("FeedbackError","#e7a0a0","#9f344d");
        Brush("EmptyInk","#93877d","#625965");
        Brush("Small", "#c6b7a7", "#403547");
        Brush("CreditInk", "#f5f0ec", "#514b57");
        Brush("ButtonFill", "#342536", "#e5d9ed");
        Brush("ButtonInk", "#fff0d5", "#30213a");
        Brush("ButtonLine", "#c49a62", "#a18aaa");
        Brush("ButtonHover", "#59402e", "#d4bee3");
        Brush("HeaderFill", "#110f14", "#f1edf4");
        Brush("HeaderLine", "#a87943", "#aa8d6b");
        Brush("ApplyFill", "#68462d", "#654477");
        Brush("ApplyLine", "#e0b36f", "#68497d");
        Brush("ApplyInk", "#fff0d5", "#ffffff");
        Brush("Muted", "#b9a08a", "#57505e");
        Brush("StatusInk", "#aaa29c", "#51465a");
        Brush("Active", "#8dd49b", "#246b41");
        Brush("ActiveDot", "#80db9a", "#348855");
        Brush("CountInk", "#f0c987", "#59376e");
        Brush("SectionInk", "#cda36d", "#59376e");
        Brush("ThemeFill", "#211824", "#ffffff");
        Brush("ThemeLine", "#ad8454", "#80648c");
        Brush("ScrollFace", "#9b7a50", "#80648c");
        Brush("ScrollTrack", "#211824", "#e9e3ee");
        Brush("ScrollArrow", "#e5bd7b", "#59376e");
        Brush("ScrollHighlight", "#c9a875", "#d5c9dd");
        // The IE9 HTA does not render these CSS gradients. Match the supplied
        // screenshot's actual flat fills, not unsupported CSS declarations.
        if (!prefs.Light)
        {
            Resources["Card"]=Resources["CardActive"]=Brushes.Transparent;
        }
        Sun.Visibility = prefs.Light ? Visibility.Collapsed : Visibility.Visible;
        Moon.Visibility = prefs.Light ? Visibility.Visible : Visibility.Collapsed;
        Theme.ToolTip = prefs.Light ? "Return to original dark theme" : "Switch to accessible light theme";
        UpdateCaption();
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left,Top,Right,Bottom; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor,Work; public uint Flags; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Auto)]
    [return:System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);

    private bool largeDesktopObserved;
    private void AdaptLayout()
    {
        // Match the HTA's screen-pixel canvas, rather than letting WPF multiply
        // every CSS-derived size by the Windows display scale a second time.
        var dpi = VisualTreeHelper.GetDpi(this);
        var client = ClientHost;
        double w = Math.Max(1,client.ActualWidth * dpi.DpiScaleX);
        double h = Math.Max(1,client.ActualHeight * dpi.DpiScaleY);
        if(w>=3000 && h>=1600) largeDesktopObserved=true;
        double desktopW=w, desktopH=h;
        IntPtr monitor=MonitorFromWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle,2);
        var info=new MonitorInfo { Size=System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
        if(GetMonitorInfo(monitor,ref info))
        { desktopW=Math.Max(w,info.Monitor.Right-info.Monitor.Left);desktopH=Math.Max(h,info.Monitor.Bottom-info.Monitor.Top); }
        if(largeDesktopObserved) {desktopW=Math.Max(desktopW,3840);desktopH=Math.Max(desktopH,2160);}
        double preferred=Math.Min(2,Math.Min(desktopW/1920,desktopH/1080));
        double zoom=Math.Round(Math.Max(1,Math.Min(preferred,Math.Min(w/1360,h/720)))*20)/20;
        CanvasRoot.LayoutTransform=new ScaleTransform(zoom/dpi.DpiScaleX,zoom/dpi.DpiScaleY);
        DialogOverlay.LayoutTransform=new ScaleTransform(zoom/dpi.DpiScaleX,zoom/dpi.DpiScaleY);
        ThemeLayer.LayoutTransform=new ScaleTransform(zoom/dpi.DpiScaleX,zoom/dpi.DpiScaleY);

        DialogFrame.MaxWidth=Math.Max(200,w/zoom*(DialogFrame.Width==590?.92:.94));
        DialogFrame.MaxHeight=Math.Max(180,h/zoom*(DialogFrame.Width==800?.90:DialogFrame.Width==590?.84:.88));
        double logicalWidth=w/zoom;
        bool compact=logicalWidth<=1100;
        Layout.Margin=new Thickness(logicalWidth*.01,compact?16:22,logicalWidth*.01,(compact?10:16)+Math.Max(0,89-Header.ActualHeight));
        // HTA inline-block columns also include whitespace between elements.
        double innerWidth=logicalWidth*.98;
        OuterLeft.Width=new GridLength(innerWidth*(compact?.005:.01)+6);
        SideWidth.Width=new GridLength(Math.Max(270,innerWidth*(compact?.22:.18)));
        ColumnGap.Width=new GridLength(innerWidth*.01+6);
        MainWidth.Width=new GridLength(1,GridUnitType.Star);
        OuterRight.Width=new GridLength(innerWidth*.01);
        bool narrowToolbar=logicalWidth<1200;
        PackToolbar.Height=narrowToolbar?64:43;
        ToolbarRule.Margin=new Thickness(0,narrowToolbar?23:32,0,0);
        PackToolbarButtons.Margin=new Thickness(0,narrowToolbar?26:5,narrowToolbar?0:22,0);
        foreach(var group in Catalogue.Children.OfType<PackPanel>()) group.InvalidateMeasure();
        Dispatcher.BeginInvoke(new Action(() => {
            ToolbarRule.Width=Math.Max(0,(Catalogue.ActualWidth+7)*.65);
            double room=Layout.ActualHeight-ActivePanel.TranslatePoint(new Point(0,0),Layout).Y-SummaryPanel.ActualHeight-Credit.ActualHeight-24;
            double chrome=ActivePanel.ActualHeight-ActiveScroll.ActualHeight;
            ActiveScroll.MaxHeight=Math.Max(55,room-chrome);
        }),System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Save()
    {
        string temp = settingsFile + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, SettingsStore.Write(prefs));
            if(File.Exists(settingsFile))File.Replace(temp,settingsFile,null);else File.Move(temp,settingsFile);
        }
        catch (Exception ex) { Notice.Text = "Settings could not be saved. " + ex.Message; Notice.Visibility=Visibility.Visible; }
        finally { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    private void Motion(FrameworkElement element, string kind)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        int frames=kind=="switch" && !prefs.Light?32:15;
        var rotation=new RotateTransform(); var scaling=new ScaleTransform(1,1);
        var transforms=new TransformGroup();transforms.Children.Add(rotation);transforms.Children.Add(scaling);
        element.RenderTransformOrigin=new Point(.5,.5);element.RenderTransform=transforms;
        var angles=new DoubleAnimationUsingKeyFrames(); var sizes=new DoubleAnimationUsingKeyFrames();var opacity=new DoubleAnimationUsingKeyFrames();
        for(int i=0;i<=frames;i++)
        {
            double t=(double)i/frames, wave=Math.Sin(2*Math.PI*t),envelope=Math.Sin(Math.PI*t);
            double angle=kind=="switch"?360*t:(kind=="theme"?17:kind=="apply"?18:24)*wave;
            double size=1+(kind=="switch"?.12:kind=="theme"?.10:kind=="apply"?.18:.23)*envelope;
            var time=KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i*43));
            angles.KeyFrames.Add(new LinearDoubleKeyFrame(angle,time));sizes.KeyFrames.Add(new LinearDoubleKeyFrame(size,time));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(1-(kind=="apply"?.12:kind=="star"?.18:0)*wave*wave,time));
        }
        angles.FillBehavior=sizes.FillBehavior=opacity.FillBehavior=FillBehavior.Stop;
        rotation.BeginAnimation(RotateTransform.AngleProperty,angles);
        scaling.BeginAnimation(ScaleTransform.ScaleXProperty,sizes);scaling.BeginAnimation(ScaleTransform.ScaleYProperty,sizes);
        element.BeginAnimation(OpacityProperty,opacity);
    }
    private void BrandHover(object sender,System.Windows.Input.MouseEventArgs e)=>Motion(BrandStar,"star");
    private void ApplyHover(object sender,System.Windows.Input.MouseEventArgs e)=>Motion(ApplyStar,"apply");
    private void ThemeHover(object sender,System.Windows.Input.MouseEventArgs e)=>Motion(ThemeMotion,"theme");
    private void PathEdited(object sender,System.Windows.Input.KeyboardFocusChangedEventArgs e)=>CommitPaths();
    private void PathKeyDown(object sender,System.Windows.Input.KeyEventArgs e)
    {if(e.Key==System.Windows.Input.Key.Enter){CommitPaths();e.Handled=true;}}
    private void CommitPaths()
    {
        if(prefs.Storage==StorageLabel.Text && prefs.Mods==ModsLabel.Text)return;
        prefs=prefs with {Storage=StorageLabel.Text,Mods=ModsLabel.Text};Save();
    }

    private void ToggleTheme(object sender, RoutedEventArgs e)
    {
        prefs = prefs with { Light = !prefs.Light };
        ApplyTheme();
        Motion(ThemeMotion,"switch");
        Save();
    }

    private void PickStorage(object sender, RoutedEventArgs e) => PickFolder(true);
    private void PickMods(object sender, RoutedEventArgs e) => PickFolder(false);
    private sealed class FolderOwner : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle {get;}
        public FolderOwner(Window window) {Handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;}
    }
    private async void PickFolder(bool storage)
    {
        try
        {
            var picker=new System.Windows.Forms.FolderBrowserDialog { Description=storage
                ? "Select your extracted The Sims 3 Expansions Mods folder.\nExample: C:\\Modding\\The Sims 3 Expansions Mods\nKeep it OUTSIDE the Electronic Arts folder."
                : "Select your active The Sims 3 Mods folder.\nTypical: C:\\Users\\YourName\\Documents\\Electronic Arts\\The Sims 3\\Mods" };
            if(picker.ShowDialog(new FolderOwner(this))!=System.Windows.Forms.DialogResult.OK)return;
            if(!Directory.Exists(picker.SelectedPath))
            {await ShowPrompt(new("Folder not selected","Please select an existing folder.",[],"OK"),false);return;}
            prefs=storage?prefs with {Storage=picker.SelectedPath}:prefs with {Mods=picker.SelectedPath};
            StorageLabel.Text=prefs.Storage;ModsLabel.Text=prefs.Mods;Save();
            // Folder changes are saved; feedback is only requested by the Refresh button.
        }
        catch {await ShowPrompt(new("Unable to browse folders","Windows could not open the folder picker. You can still type or paste the path manually.",[],"OK"),false);}
    }

    private void RefreshClick(object sender,RoutedEventArgs e) {CommitPaths();Refresh(true);}
    private void Refresh(bool showFeedback=false)
    {
        StorageLabel.Text=prefs.Storage;ModsLabel.Text=prefs.Mods;
        var state=engine.Inspect(prefs.Storage,prefs.Mods);
        bool updated=refreshDisplay.Update(state,showFeedback,DateTime.Now);
        if(showFeedback && refreshDisplay.Feedback is {} feedback)
        {
            Notice.Text=feedback.Text;
            Notice.SetResourceReference(TextBlock.ForegroundProperty,feedback.Error?"FeedbackError":"FeedbackSuccess");
            Notice.Visibility=Visibility.Visible;
        }
        if(!updated){if(IsLoaded)AdaptLayout();return;}
        ActiveList.Items.Clear();
        foreach (var card in cards)
        {
            string code = (string)card.Tag;
            string id = code.Split('_')[0];
            bool active = state.ActiveCodes.Contains(code);
            var label = (TextBlock)FindName("Status" + id);
            label.Text = active ? "ACTIVE" : "NOT ACTIVE";
            label.SetResourceReference(ForegroundProperty, active ? "Active" : "StatusInk");
            ((Border)FindName("Card" + id)).SetResourceReference(Border.BorderBrushProperty, active ? "CardActiveLine" : "CardLine");
            ((Border)FindName("Card"+id)).SetResourceReference(Border.BackgroundProperty,active?"CardActive":"Card");
            ((Border)FindName("Inset"+id)).SetResourceReference(Border.BorderBrushProperty,active?"ActiveInset":"CardInset");
            var dot=(System.Windows.Shapes.Ellipse)FindName("Dot"+id);
            dot.Visibility=Visibility.Visible;
            dot.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,active?"ActiveDot":"StatusInk");
            if(active)dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,"ActiveDot");else dot.Fill=Brushes.Transparent;
            if (active) {
                string title = (string)card.ToolTip;
                var item=new TextBlock {FontSize=15,TextWrapping=TextWrapping.Wrap,LineHeight=30.1,LineStackingStrategy=LineStackingStrategy.BlockLineHeight};
                item.SetResourceReference(TextBlock.ForegroundProperty,"ActiveListInk");
                var gem=new System.Windows.Documents.Run("◆") {FontSize=17};
                gem.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty,"ActiveGem");
                item.Inlines.Add(gem);item.Inlines.Add(" "+title);ActiveList.Items.Add(item);
            }
        }
        if (state.ActiveCodes.Length==0)
        {
            var empty=new TextBlock { Text=HtaMessages.Empty, FontSize=15,
                TextWrapping=TextWrapping.Wrap, LineHeight=26.55, LineStackingStrategy=LineStackingStrategy.BlockLineHeight };
            empty.SetResourceReference(TextBlock.ForegroundProperty,"EmptyInk");
            ActiveList.Items.Add(empty);
        }
        Summary.Text = state.ActiveCodes.Length + " managed pack(s) found";
        if(IsLoaded)AdaptLayout();
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int key, ref int color, int size);
    private void UpdateCaption()
    {
        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || Environment.OSVersion.Version.Build<22000) return;
        int background = prefs.Light ? 0xF4EDF1 : 0x241821;
        int foreground = prefs.Light ? 0x6E3759 : 0x7BBDE5;
        DwmSetWindowAttribute(handle,35,ref background,4);
        DwmSetWindowAttribute(handle,36,ref foreground,4);
    }
    private void OpenStructure(object sender, RoutedEventArgs e) {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://thetinyscholar.tumblr.com/post/832009571557163008/tinyscholars-sims-3-pack-mods-folder-switcher") { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception ex) { MessageBox.Show("Could not open the folder structure link: " + ex.Message); }
    }
    private void SelectionChanged(object sender, RoutedEventArgs e) => UpdateCount();
    private void UpdateCount() => Count.Text = cards.Count(card => card.IsChecked == true).ToString();
    private void SelectAll(object sender, RoutedEventArgs e) { foreach (var card in cards) card.IsChecked = true; }
    private void Clear(object sender, RoutedEventArgs e) { foreach (var card in cards) card.IsChecked = false; }
}
