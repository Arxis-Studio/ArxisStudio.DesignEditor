using Avalonia;
using Avalonia.Controls;
using ArxisStudio.Attached;
using Avalonia.VisualTree;

namespace ArxisStudio.Controls;

/// <summary>
/// Панель для абсолютного позиционирования дочерних элементов.
/// <para>
/// Размещает элементы согласно присоединенным свойствам <see cref="Layout.XProperty"/> и <see cref="Layout.YProperty"/>.
/// Используется как базовый контейнер для построения пользовательских интерфейсов в редакторе.
/// </para>
/// </summary>
public class AbsolutePanel : Panel
{
    /// <summary>
    /// Определяет свойство <see cref="Extent"/>.
    /// </summary>
    public static readonly StyledProperty<Rect> ExtentProperty =
        AvaloniaProperty.Register<AbsolutePanel, Rect>(nameof(Extent));

    /// <summary>
    /// Получает прямоугольник, охватывающий все дочерние элементы.
    /// Используется родительскими элементами (например, DesignEditor) для вычисления области прокрутки.
    /// </summary>
    public Rect Extent
    {
        get => GetValue(ExtentProperty);
        set => SetValue(ExtentProperty, value);
    }

    static AbsolutePanel()
    {
        // Подписываемся на изменения координат Layout.X/Y у дочерних элементов,
        // чтобы вызвать пересчет макета (InvalidateLayout) данной панели.
        Layout.XProperty.Changed.AddClassHandler<Control>((s, e) => InvalidateParentLayout(s));
        Layout.YProperty.Changed.AddClassHandler<Control>((s, e) => InvalidateParentLayout(s));
    }

    /// <summary>
    /// Вызывает перерисовку панели, если измененный элемент находится внутри AbsolutePanel.
    /// </summary>
    private static void InvalidateParentLayout(Control control)
    {
        if (control.GetVisualParent() is AbsolutePanel panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        double minX = 0, minY = 0;
        double maxX = 0, maxY = 0;
        bool hasItems = false;

        foreach (var child in Children)
        {
            // Измеряем ребенка, предоставляя ему неограниченное пространство
            child.Measure(infinite);

            double x = Layout.GetX(child);
            double y = Layout.GetY(child);

            // Если координаты не заданы (NaN), считаем их равными 0 для расчета границ
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

        // Вычисляем область, занимаемую элементами
        var extent = hasItems ? new Rect(minX, minY, maxX - minX, maxY - minY) : new Rect();
        SetCurrentValue(ExtentProperty, extent);

        // ВАЖНО: Возвращаем реальный размер содержимого.
        // Это необходимо для корректной работы HitTest (выделения мышью) в родительском ItemsPresenter.
        return new Size(maxX, maxY);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            double x = Layout.GetX(child);
            double y = Layout.GetY(child);

            // Если координаты не заданы, размещаем в точке (0,0)
            double finalX = double.IsNaN(x) ? 0 : x;
            double finalY = double.IsNaN(y) ? 0 : y;

            // Размещаем элемент точно по заданным координатам без использования Margin
            child.Arrange(new Rect(new Point(finalX, finalY), child.DesiredSize));
        }
        return finalSize;
    }
}
