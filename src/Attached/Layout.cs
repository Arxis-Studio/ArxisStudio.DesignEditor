using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ArxisStudio.Controls;

namespace ArxisStudio.Attached;

/// <summary>
/// Предоставляет присоединенные свойства и логику для системы позиционирования дизайнера.
/// </summary>
public static class Layout
{
    private static int _isInsidePositionChange;

    #region Attached Properties

    /// <summary>
    /// Локальная координата X элемента относительно его родителя.
    /// Влияет на размещение внутри <see cref="AbsolutePanel"/>.
    /// </summary>
    public static readonly AttachedProperty<double> XProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "X", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Локальная координата Y элемента относительно его родителя.
    /// Влияет на размещение внутри <see cref="AbsolutePanel"/>.
    /// </summary>
    public static readonly AttachedProperty<double> YProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "Y", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Глобальная координата X относительно поверхности дизайнера (<see cref="DesignSurface"/>).
    /// Используется для отображения рамок выделения (Adorners) и инспектора свойств.
    /// </summary>
    public static readonly AttachedProperty<double> DesignXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignX", typeof(Layout), 0d, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Глобальная координата Y относительно поверхности дизайнера (<see cref="DesignSurface"/>).
    /// Используется для отображения рамок выделения (Adorners) и инспектора свойств.
    /// </summary>
    public static readonly AttachedProperty<double> DesignYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignY", typeof(Layout), 0d, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    #endregion

    static Layout()
    {
        // При изменении локальных координат подписываемся на обновление макета
        XProperty.Changed.AddClassHandler<Control>((s, e) => RegisterForLayoutUpdates(s));
        YProperty.Changed.AddClassHandler<Control>((s, e) => RegisterForLayoutUpdates(s));

        // При изменении глобальных координат (например, перетаскивание рамки) обновляем локальные
        DesignXProperty.Changed.AddClassHandler<Control>((s, e) => OnDesignPositionChanged(s));
        DesignYProperty.Changed.AddClassHandler<Control>((s, e) => OnDesignPositionChanged(s));
    }

    private static void RegisterForLayoutUpdates(Control? control)
    {
        if (control == null) return;
        control.LayoutUpdated -= OnLayoutUpdated;
        control.LayoutUpdated += OnLayoutUpdated;

        UpdateDesignPosition(control);
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is Control control)
        {
            UpdateDesignPosition(control);
        }
    }

    /// <summary>
    /// Пересчитывает глобальные координаты (DesignX/Y) относительно корня редактора.
    /// </summary>
    private static void UpdateDesignPosition(Control control)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;
            try
            {
                // Ищем DesignSurface (корень редактора). Игнорируем вложенные AbsolutePanel.
                Visual? reference = control.FindAncestorOfType<DesignSurface>()
                                    ?? control.FindAncestorOfType<DesignEditor>() as Visual;

                if (reference != null)
                {
                    var position = control.TranslatePoint(new Point(0, 0), reference);

                    if (position.HasValue)
                    {
                        // Обновляем Design-свойства только при реальном изменении (фильтр шума float)
                        if (Math.Abs(GetDesignX(control) - position.Value.X) > 0.01)
                            SetDesignX(control, position.Value.X);

                        if (Math.Abs(GetDesignY(control) - position.Value.Y) > 0.01)
                            SetDesignY(control, position.Value.Y);
                    }
                }
            }
            finally { Interlocked.Exchange(ref _isInsidePositionChange, 0); }
        }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Конвертирует изменение глобальных координат обратно в локальные.
    /// </summary>
    private static void OnDesignPositionChanged(Control? control)
    {
        if (control == null || Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;
        try
        {
             if (control.GetVisualRoot() is null) return;

             Visual? root = control.FindAncestorOfType<DesignSurface>()
                            ?? control.FindAncestorOfType<DesignEditor>() as Visual;

             var parent = control.GetVisualParent();

             if (root != null && parent != null)
             {
                 var dx = GetDesignX(control);
                 var dy = GetDesignY(control);

                 // Переводим точку из координат корня в координаты непосредственного родителя
                 var local = root.TranslatePoint(new Point(dx, dy), parent);

                 if (local.HasValue)
                 {
                     SetX(control, local.Value.X);
                     SetY(control, local.Value.Y);
                 }
             }
        }
        finally { Interlocked.Exchange(ref _isInsidePositionChange, 0); }
    }

    #region Accessors

    public static double GetX(AvaloniaObject o) => o.GetValue(XProperty);
    public static void SetX(AvaloniaObject o, double v) => o.SetValue(XProperty, v);

    public static double GetY(AvaloniaObject o) => o.GetValue(YProperty);
    public static void SetY(AvaloniaObject o, double v) => o.SetValue(YProperty, v);

    public static double GetDesignX(AvaloniaObject o) => o.GetValue(DesignXProperty);
    public static void SetDesignX(AvaloniaObject o, double v) => o.SetValue(DesignXProperty, v);

    public static double GetDesignY(AvaloniaObject o) => o.GetValue(DesignYProperty);
    public static void SetDesignY(AvaloniaObject o, double v) => o.SetValue(DesignYProperty, v);

    #endregion
}
