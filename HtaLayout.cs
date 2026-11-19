using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TinyScholar.Switcher;

// CSS tracking is expressed in logical pixels; WPF has no equivalent on TextBlock.
public sealed class TrackedText : Control
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(TrackedText), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackingProperty = DependencyProperty.Register(nameof(Tracking), typeof(double), typeof(TrackedText), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty WrapProperty = DependencyProperty.Register(nameof(Wrap), typeof(bool), typeof(TrackedText), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty,value); }
    public double Tracking { get => (double)GetValue(TrackingProperty); set => SetValue(TrackingProperty,value); }
    public bool Wrap { get => (bool)GetValue(WrapProperty); set => SetValue(WrapProperty,value); }
    private FormattedText Format(string s) => new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground ?? Brushes.Black,
        null, TextFormattingMode.Display, EffectivePixelsPerDip());
    private double EffectivePixelsPerDip()
    {
        var source=PresentationSource.FromVisual(this);
        if(source?.RootVisual is Visual root && source.CompositionTarget!=null)
        {
            var transform=TransformToAncestor(root);
            var vector=transform.Transform(new Point(1,0))-transform.Transform(new Point(0,0));
            vector=source.CompositionTarget.TransformToDevice.Transform(vector);
            if(vector.Length>0)return vector.Length;
        }
        return VisualTreeHelper.GetDpi(this).PixelsPerDip;
    }
    private double WidthOf(string s) => s.Sum(c => Format(c.ToString()).WidthIncludingTrailingWhitespace + Tracking);
    private List<string> Lines(double width)
    {
        if (!Wrap || double.IsInfinity(width)) return new() { Text };
        var lines = new List<string>(); string line = "";
        foreach (var word in Text.Split(' '))
        {
            string next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && WidthOf(next) > width) { lines.Add(line); line = word; }
            else line = next;
        }
        lines.Add(line); return lines;
    }
    protected override Size MeasureOverride(Size available)
    {
        var lines = Lines(available.Width);
        return new Size(Math.Min(available.Width,lines.Max(WidthOf)), lines.Count * FontSize * 1.2);
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double y=0;
        foreach (string line in Lines(ActualWidth))
        {
            double x=0;
            foreach(char c in line) { var f=Format(c.ToString()); dc.DrawText(f,new Point(x,y)); x+=f.WidthIncludingTrailingWhitespace+Tracking; }
            y+=FontSize*1.2;
        }
    }
}

// Preserve the wide layout; choose fewer columns before cards become cramped.
public sealed class PackPanel : Panel
{
    private static int Columns(double width) => width*.2375>=240 ? 4 : Math.Max(1,Math.Min(3,(int)Math.Floor((width+width*.0125)/(240+width*.0125))));
    private static double CardWidth(double width,int columns) => columns==4 ? width*.2375 : (width-(columns-1)*width*.0125)/columns;
    protected override Size MeasureOverride(Size available)
    {
        double width=double.IsInfinity(available.Width)?900:available.Width;
        int columns=Columns(width);
        double cardWidth=CardWidth(width,columns); double y=0,row=0; int n=0;
        foreach(UIElement child in InternalChildren)
        {
            child.Measure(new Size(cardWidth,double.PositiveInfinity));
            row=Math.Max(row,child.DesiredSize.Height);
            if(++n%columns==0) {y+=row+9;row=0;}
        }
        if(n%columns!=0)y+=row+9;
        return new Size(width,y);
    }
    protected override Size ArrangeOverride(Size final)
    {
        int columns=Columns(final.Width);
        double cardWidth=CardWidth(final.Width,columns), gap=final.Width*.0125;double y=0;
        for(int i=0;i<InternalChildren.Count;i+=columns)
        {
            double row=0;
            for(int j=i;j<Math.Min(i+columns,InternalChildren.Count);j++)row=Math.Max(row,InternalChildren[j].DesiredSize.Height);
            for(int j=i;j<Math.Min(i+columns,InternalChildren.Count);j++)
            {var child=InternalChildren[j];child.Arrange(new Rect(Math.Round((j-i)*(cardWidth+gap)),Math.Round(y),Math.Round(cardWidth),Math.Ceiling(child.DesiredSize.Height)));}
            y+=row+9;
        }
        return final;
    }
}
