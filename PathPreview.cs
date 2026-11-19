using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace TinyScholar.Switcher;
// The HTA displays a measured prefix followed by three periods while unfocused.
public sealed class PathPreview : TextBlock
{
    public static readonly DependencyProperty FullPathProperty=DependencyProperty.Register(nameof(FullPath),typeof(string),typeof(PathPreview),new PropertyMetadata("",Changed));
    public string FullPath {get=>(string)GetValue(FullPathProperty);set=>SetValue(FullPathProperty,value);}
    public PathPreview(){SizeChanged+=(_,_)=>Update();Loaded+=(_,_)=>Update();}
    private static void Changed(DependencyObject item,DependencyPropertyChangedEventArgs e)=>((PathPreview)item).Update();
    private void Update()
    {
        string full=FullPath??"";
        if(ActualWidth<=0){Text=full;return;}
        double Width(string value)=>new FormattedText(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,
            new Typeface(FontFamily,FontStyle,FontWeight,FontStretch),FontSize,Foreground??Brushes.Black,
            null,TextFormattingMode.Display,VisualTreeHelper.GetDpi(this).PixelsPerDip).WidthIncludingTrailingWhitespace;
        if(Width(full)<=ActualWidth){Text=full;return;}
        int lo=0,hi=full.Length;
        while(lo<hi){int mid=(lo+hi+1)/2;if(Width(full.Substring(0,mid)+"...")<=ActualWidth)lo=mid;else hi=mid-1;}
        Text=full.Substring(0,lo)+"...";
    }
}
