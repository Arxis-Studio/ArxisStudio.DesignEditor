using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ArxisStudio.Controls;

namespace ArxisStudio.Attached;

public static class Layout
{
    private static int _isInsidePositionChange;

    #region Attached Properties

    public static readonly AttachedProperty<double> XProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "X", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<double> YProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "Y", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<double> DesignXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignX", typeof(Layout), 0d, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    public static readonly AttachedProperty<double> DesignYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignY", typeof(Layout), 0d, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    #endregion

    static Layout()
    {
        // Если меняем X/Y -> начинаем следить за элементом
        XProperty.Changed.AddClassHandler<Control>((s, e) => Track(s));
        YProperty.Changed.AddClassHandler<Control>((s, e) => Track(s));

        // Обратная связь от адорнеров
        DesignXProperty.Changed.AddClassHandler<Control>((s, e) => OnDesignPositionChanged(s));
        DesignYProperty.Changed.AddClassHandler<Control>((s, e) => OnDesignPositionChanged(s));
    }

    /// <summary>
    /// Начинает отслеживать перемещение элемента для расчета глобальных координат.
    /// Вызывается автоматически при изменении X/Y или вручную из AbsolutePanel.
    /// </summary>
    public static void Track(Control? control)
    {
        if (control == null) return;

        // Переподписываемся, чтобы избежать дублирования
        control.LayoutUpdated -= OnLayoutUpdated;
        control.LayoutUpdated += OnLayoutUpdated;

        // Форсируем расчет прямо сейчас
        UpdateDesignPosition(control);
    }

    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is Control control)
        {
            UpdateDesignPosition(control);
        }
    }

    private static void UpdateDesignPosition(Control control)
    {
        // Используем Post, чтобы дать верстке завершиться перед расчетом координат
        Dispatcher.UIThread.Post(() =>
        {
            if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;
            try
            {
                // Ищем корень (DesignSurface)
                Visual? reference = control.FindAncestorOfType<DesignSurface>()
                                    ?? control.FindAncestorOfType<DesignEditor>() as Visual;

                if (reference != null)
                {
                    // Считаем реальное положение элемента на экране относительно корня
                    var position = control.TranslatePoint(new Point(0, 0), reference);

                    if (position.HasValue)
                    {
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
