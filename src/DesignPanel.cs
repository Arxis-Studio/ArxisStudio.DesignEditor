using Avalonia;
using Avalonia.Controls;

namespace ArxisStudio;

public class DesignPanel : Panel
{
    // Свойство для передачи размера контента в редактор (Binding OneWayToSource)
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

            // Читаем позицию: либо быстро через интерфейс, либо через Canvas-свойства
            Point loc = (child is IDesignEditorItem item) ? item.Location :
                        new Point(child.GetValue(Canvas.LeftProperty), child.GetValue(Canvas.TopProperty));

            var size = child.DesiredSize;

            if (size.Width > 0 && size.Height > 0)
            {
                hasItems = true;
                minX = Math.Min(minX, loc.X);
                minY = Math.Min(minY, loc.Y);
                maxX = Math.Max(maxX, loc.X + size.Width);
                maxY = Math.Max(maxY, loc.Y + size.Height);
            }
        }

        // Обновляем Extent, который уйдет в DesignEditor
        if (hasItems)
        {
            SetCurrentValue(ExtentProperty, new Rect(minX, minY, maxX - minX, maxY - minY));
        }
        else
        {
            SetCurrentValue(ExtentProperty, new Rect(0,0,0,0));
        }

        return new Size(); // Панель не занимает места (Infinite)
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is IDesignEditorItem designItem)
            {
                // Быстрый путь
                child.Arrange(new Rect(designItem.Location, child.DesiredSize));
            }
            else
            {
                // Медленный путь (Fallback)
                double x = child.GetValue(Canvas.LeftProperty);
                double y = child.GetValue(Canvas.TopProperty);
                child.Arrange(new Rect(new Point(x, y), child.DesiredSize));
            }
        }
        return finalSize;
    }
}
