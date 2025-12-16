using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ArxisStudio.Attached;
using Avalonia.VisualTree;

namespace ArxisStudio.Controls;

public class AbsolutePanel : Panel
{
    public static readonly StyledProperty<Rect> ExtentProperty =
        AvaloniaProperty.Register<AbsolutePanel, Rect>(nameof(Extent));

    public Rect Extent
    {
        get => GetValue(ExtentProperty);
        set => SetValue(ExtentProperty, value);
    }

    static AbsolutePanel()
    {
        Layout.XProperty.Changed.AddClassHandler<Control>((s, e) => InvalidateParentLayout(s));
        Layout.YProperty.Changed.AddClassHandler<Control>((s, e) => InvalidateParentLayout(s));
    }

    private static void InvalidateParentLayout(Control control)
    {
        if (control.GetVisualParent() is AbsolutePanel panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double minX = 0, minY = 0;
        double maxX = 0, maxY = 0;
        bool hasItems = false;

        foreach (var child in Children)
        {
            child.Measure(infinite);

            double x = Layout.GetX(child);
            double y = Layout.GetY(child);

            double effectiveX = double.IsNaN(x) ? 0 : x;
            double effectiveY = double.IsNaN(y) ? 0 : y;

            var size = child.DesiredSize;

            if (size.Width > 0 && size.Height > 0)
            {
                hasItems = true;
                if (effectiveX < minX) minX = effectiveX;
                if (effectiveY < minY) minY = effectiveY;
                if (effectiveX + size.Width > maxX) maxX = effectiveX + size.Width;
                if (effectiveY + size.Height > maxY) maxY = effectiveY + size.Height;
            }
        }

        var extent = hasItems ? new Rect(minX, minY, maxX - minX, maxY - minY) : new Rect();
        SetCurrentValue(ExtentProperty, extent);

        return new Size(maxX, maxY);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            // ВАЖНО: Принудительно включаем слежение за глобальными координатами для каждого ребенка.
            // Это чинит проблему, когда DesignX/Y не обновлялись для элементов без явных Layout.X/Y.
            Layout.Track(child);

            double x = Layout.GetX(child);
            double y = Layout.GetY(child);

            // Расчет X
            double finalX = 0;
            double finalW = child.DesiredSize.Width;

            if (!double.IsNaN(x))
            {
                finalX = x;
            }
            else
            {
                switch (child.HorizontalAlignment)
                {
                    case HorizontalAlignment.Center:
                        finalX = (finalSize.Width - child.DesiredSize.Width) / 2;
                        break;
                    case HorizontalAlignment.Right:
                        finalX = finalSize.Width - child.DesiredSize.Width;
                        break;
                    case HorizontalAlignment.Stretch:
                        finalX = 0;
                        finalW = finalSize.Width;
                        break;
                    default: // Left
                        finalX = 0;
                        break;
                }
            }

            // Расчет Y
            double finalY = 0;
            double finalH = child.DesiredSize.Height;

            if (!double.IsNaN(y))
            {
                finalY = y;
            }
            else
            {
                switch (child.VerticalAlignment)
                {
                    case VerticalAlignment.Center:
                        finalY = (finalSize.Height - child.DesiredSize.Height) / 2;
                        break;
                    case VerticalAlignment.Bottom:
                        finalY = finalSize.Height - child.DesiredSize.Height;
                        break;
                    case VerticalAlignment.Stretch:
                        finalY = 0;
                        finalH = finalSize.Height;
                        break;
                    default: // Top
                        finalY = 0;
                        break;
                }
            }

            child.Arrange(new Rect(finalX, finalY, finalW, finalH));
        }
        return finalSize;
    }
}
