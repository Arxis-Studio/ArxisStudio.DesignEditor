using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;

namespace ArxisStudio;

public class DesignEditorItem : ContentControl, ISelectable, IDesignEditorItem
{
    // --- Selection ---
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<DesignEditorItem, bool>(nameof(IsSelected));

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    // --- Location (Fast Property) ---
    public static readonly StyledProperty<Point> LocationProperty =
        AvaloniaProperty.Register<DesignEditorItem, Point>(nameof(Location));

    public Point Location
    {
        get => GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    static DesignEditorItem()
    {
        SelectableMixin.Attach<DesignEditorItem>(IsSelectedProperty);
        FocusableProperty.OverrideDefaultValue<DesignEditorItem>(true);

        // Явное уведомление родительской панели о перемещении элемента
        LocationProperty.Changed.AddClassHandler<DesignEditorItem>((item, args) =>
        {
            if (item.Parent is Panel panel)
            {
                panel.InvalidateArrange();
            }
        });
    }
}
