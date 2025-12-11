using Avalonia;
using Avalonia.Controls;

namespace ArxisStudio;

public class DesignPanel : Panel
{
    public static readonly StyledProperty<Rect> ExtentProperty =
        AvaloniaProperty.Register<DesignPanel, Rect>(nameof(Extent));

    public Rect Extent
    {
        get => GetValue(ExtentProperty);
        set => SetValue(ExtentProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasItems = false;

        foreach (var child in Children)
        {
            child.Measure(infinite);
            Point loc = (child is IDesignEditorItem item) ? item.Location :
                        new Point(child.GetValue(Canvas.LeftProperty), child.GetValue(Canvas.TopProperty));
            var size = child.DesiredSize;

            if (size.Width > 0 && size.Height > 0)
            {
                hasItems = true;
                if (loc.X < minX) minX = loc.X;
                if (loc.Y < minY) minY = loc.Y;
                if (loc.X + size.Width > maxX) maxX = loc.X + size.Width;
                if (loc.Y + size.Height > maxY) maxY = loc.Y + size.Height;
            }
        }

        SetCurrentValue(ExtentProperty, hasItems ? new Rect(minX, minY, maxX - minX, maxY - minY) : new Rect());
        return new Size();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is IDesignEditorItem designItem)
                child.Arrange(new Rect(designItem.Location, child.DesiredSize));
            else
            {
                double x = child.GetValue(Canvas.LeftProperty);
                double y = child.GetValue(Canvas.TopProperty);
                child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
            }
        }
        return finalSize;
    }
}
