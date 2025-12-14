using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace ArxisStudio.Attached;

/// <summary>
/// Система абсолютного позиционирования элементов для Дизайнера UI.
/// <para>
/// Позволяет свободно перемещать элементы внутри любых контейнеров.
/// </para>
/// <list type="bullet">
/// <item>Внутри <see cref="Canvas"/>: Использует <see cref="Canvas.LeftProperty"/> и <see cref="Canvas.TopProperty"/>.</item>
/// <item>Внутри Grid/Panel/Border: Использует <see cref="Layoutable.MarginProperty"/>.</item>
/// </list>
/// <para>
/// Автоматически вычисляет глобальные координаты (<see cref="DesignXProperty"/>, <see cref="DesignYProperty"/>)
/// относительно <see cref="DesignEditor"/> для синхронизации с верхним слоем (Adorner Layer).
/// </para>
/// </summary>
public static class Layout
{
    // Защита от рекурсивного переполнения стека (StackOverflow) при взаимных изменениях свойств.
    private static int _isInsidePositionChange;

    // Флаг, указывающий, что мы обновляем свойства X/Y, считывая их из реального макета (Read Mode).
    // В этом режиме мы не должны пытаться применять их обратно к Margin (Write Mode).
    [ThreadStatic]
    private static bool _isUpdatingReadOnlyValues;

    #region Attached Properties

    /// <summary>
    /// Локальная координата X относительно родительского контейнера.
    /// Меняет Canvas.Left или Margin.Left.
    /// </summary>
    public static readonly AttachedProperty<double> XProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "X", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Локальная координата Y относительно родительского контейнера.
    /// Меняет Canvas.Top или Margin.Top.
    /// </summary>
    public static readonly AttachedProperty<double> YProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "Y", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Глобальная координата X относительно корня дизайнера (DesignEditor).
    /// Используется для привязки рамок выделения (Adorners) на верхнем слое.
    /// </summary>
    public static readonly AttachedProperty<double> DesignXProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignX", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Глобальная координата Y относительно корня дизайнера (DesignEditor).
    /// Используется для привязки рамок выделения (Adorners) на верхнем слое.
    /// </summary>
    public static readonly AttachedProperty<double> DesignYProperty =
        AvaloniaProperty.RegisterAttached<Control, double>(
            "DesignY", typeof(Layout), double.NaN, inherits: false, defaultBindingMode: BindingMode.TwoWay);

    #endregion

    static Layout()
    {
        // Используем AddClassHandler для подписки без System.Reactive (совместимо с netstandard2.0)

        // Подписка на изменение локальных координат (логика перемещения)
        XProperty.Changed.AddClassHandler<Control>((s, e) => OnPositionChanged(s));
        YProperty.Changed.AddClassHandler<Control>((s, e) => OnPositionChanged(s));

        // Подписка на изменение глобальных координат (обратная связь от перетаскивания рамки)
        DesignXProperty.Changed.AddClassHandler<Control>((s, e) => OnDesignPositionChanged(s));
        DesignYProperty.Changed.AddClassHandler<Control>((s, e) => OnDesignPositionChanged(s));

        // Подписка на изменение выравнивания (логика Stretch -> Auto)
        Layoutable.HorizontalAlignmentProperty.Changed.AddClassHandler<Control>((s, e) => OnAlignmentChanged(s));
        Layoutable.VerticalAlignmentProperty.Changed.AddClassHandler<Control>((s, e) => OnAlignmentChanged(s));
    }

    /// <summary>
    /// Вызывается при изменении локальных свойств X или Y (через код, биндинг или инспектор свойств).
    /// </summary>
    private static void OnPositionChanged(Control? control)
    {
        if (control == null) return;

        // Если мы в режиме чтения (обновляемся из LayoutUpdated), пропускаем логику применения.
        if (_isUpdatingReadOnlyValues)
        {
            UpdateDesignPosition(control);
            return;
        }

        // Блокировка рекурсии
        if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;

        try
        {
            // Подписываемся на LayoutUpdated, чтобы следить за изменениями макета извне (например, изменение Grid)
            // -= и += гарантируют, что подписка не дублируется.
            control.LayoutUpdated -= OnLayoutUpdated;
            control.LayoutUpdated += OnLayoutUpdated;

            // 1. Подготовка: если элемент растянут (Stretch), фиксируем его размер и переключаем на Left/Top
            EnsureManualPositioningMode(control);

            // 2. Применение: устанавливаем Margin или Canvas координаты
            ApplyPosition(control);

            // 3. Синхронизация: обновляем глобальные координаты для слоя рамок
            UpdateDesignPosition(control);
        }
        finally
        {
            Interlocked.Exchange(ref _isInsidePositionChange, 0);
        }
    }

    /// <summary>
    /// Вызывается при изменении DesignX/DesignY (например, когда пользователь тащит рамку выделения).
    /// Транслирует глобальные координаты обратно в локальные (X/Y).
    /// </summary>
    private static void OnDesignPositionChanged(Control? control)
    {
        if (control == null || Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;

        try
        {
            // Если контрол еще не в визуальном дереве, ждем загрузки
            if (control.GetVisualRoot() is null)
            {
                void Handler(object? s, VisualTreeAttachmentEventArgs e)
                {
                    control.AttachedToVisualTree -= Handler;
                    Dispatcher.UIThread.Post(() => OnDesignPositionChanged(control), DispatcherPriority.Loaded);
                }
                control.AttachedToVisualTree += Handler;
                return;
            }

            // Ищем DesignEditor (наш холст) или корневой элемент окна
            Visual? root = control.FindAncestorOfType<DesignEditor>() as Visual
                           ?? control.GetVisualRoot() as Visual;

            var parent = control.GetVisualParent();

            if (root == null || parent == null) return;

            var globalX = GetDesignX(control);
            var globalY = GetDesignY(control);

            if (!double.IsNaN(globalX) && !double.IsNaN(globalY))
            {
                // Математика: Глобальная точка (Root) -> Локальная точка (Parent)
                var globalPoint = new Point(globalX, globalY);
                var localPoint = root.TranslatePoint(globalPoint, parent);

                if (localPoint.HasValue)
                {
                    // Устанавливаем локальные X/Y.
                    // Это вызовет OnPositionChanged, но рекурсия заблокирована флагом _isInsidePositionChange.
                    SetX(control, localPoint.Value.X);
                    SetY(control, localPoint.Value.Y);

                    EnsureManualPositioningMode(control);
                    ApplyPosition(control);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isInsidePositionChange, 0);
        }
    }

    /// <summary>
    /// Применяет координаты к реальным свойствам контрола.
    /// </summary>
    private static void ApplyPosition(Control control)
    {
        var x = GetX(control);
        var y = GetY(control);

        // Если координаты не заданы (NaN), мы не управляем позицией
        if (double.IsNaN(x) && double.IsNaN(y)) return;

        // ВАРИАНТ 1: Мы внутри Canvas
        if (IsInsideCanvas(control))
        {
            if (!double.IsNaN(x)) Canvas.SetLeft(control, x);
            if (!double.IsNaN(y)) Canvas.SetTop(control, y);

            // Сбрасываем Margin в 0, чтобы он не добавлял лишних отступов к Canvas-координатам
            control.Margin = new Thickness(0);
        }
        // ВАРИАНТ 2: Мы внутри Grid, StackPanel, Border и т.д.
        else
        {
            // Для корректной работы Margin как координат, элемент должен быть прижат к левому верхнему углу
            if (control.HorizontalAlignment != HorizontalAlignment.Left)
                control.HorizontalAlignment = HorizontalAlignment.Left;

            if (control.VerticalAlignment != VerticalAlignment.Top)
                control.VerticalAlignment = VerticalAlignment.Top;

            // Берем текущий Margin как базу
            double currentLeft = control.Margin.Left;
            double currentTop = control.Margin.Top;

            double newLeft = double.IsNaN(x) ? currentLeft : x;
            double newTop = double.IsNaN(y) ? currentTop : y;

            // Устанавливаем Margin. Right и Bottom игнорируем (0), так как позиционируем от Top-Left.
            control.Margin = new Thickness(newLeft, newTop, 0, 0);
        }
    }

    /// <summary>
    /// Подготавливает элемент к ручному позиционированию.
    /// Логика: Если элемент был растянут (Stretch), он не может иметь координаты.
    /// Мы должны зафиксировать его текущий размер (Bounds) и переключить выравнивание на Left/Top.
    /// </summary>
    private static void EnsureManualPositioningMode(Control control)
    {
        // Canvas не требует переключения Alignment
        if (IsInsideCanvas(control)) return;

        bool hasX = !double.IsNaN(GetX(control));
        bool hasY = !double.IsNaN(GetY(control));

        // --- Обработка ширины ---
        if (hasX)
        {
            if (control.HorizontalAlignment == HorizontalAlignment.Stretch)
            {
                // Запоминаем текущую ширину перед отключением Stretch
                if (double.IsNaN(control.Width))
                    control.Width = control.Bounds.Width;

                control.HorizontalAlignment = HorizontalAlignment.Left;
            }
            else if (control.HorizontalAlignment != HorizontalAlignment.Left)
            {
                control.HorizontalAlignment = HorizontalAlignment.Left;
            }
        }

        // --- Обработка высоты ---
        if (hasY)
        {
            if (control.VerticalAlignment == VerticalAlignment.Stretch)
            {
                // Запоминаем текущую высоту перед отключением Stretch
                if (double.IsNaN(control.Height))
                    control.Height = control.Bounds.Height;

                control.VerticalAlignment = VerticalAlignment.Top;
            }
            else if (control.VerticalAlignment != VerticalAlignment.Top)
            {
                control.VerticalAlignment = VerticalAlignment.Top;
            }
        }
    }

    /// <summary>
    /// Реакция на изменение Alignment извне.
    /// Если пользователь выбрал Stretch, сбрасываем фиксированные размеры в Auto.
    /// </summary>
    private static void OnAlignmentChanged(Control? control)
    {
        if (control == null) return;

        if (control.HorizontalAlignment == HorizontalAlignment.Stretch)
            control.Width = double.NaN; // Auto

        if (control.VerticalAlignment == VerticalAlignment.Stretch)
            control.Height = double.NaN; // Auto

        // Повторно применяем позицию, чтобы убедиться в корректности состояния
        ApplyPosition(control);
    }

    /// <summary>
    /// Обратная связь: вызывается движком Avalonia после завершения компоновки (Measure & Arrange).
    /// Если Margin изменился (или Grid сдвинул элементы), мы обновляем наши свойства X и Y.
    /// </summary>
    private static void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (sender is not Control control) return;

        // Если мы сами сейчас меняем позицию (Write Mode), игнорируем это событие
        if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;

        try
        {
            var parent = control.GetVisualParent();
            if (parent != null)
            {
                // 1. Получаем реальную позицию элемента в пикселях относительно родителя
                var pos = control.TranslatePoint(new Point(0, 0), parent);
                if (pos.HasValue)
                {
                    _isUpdatingReadOnlyValues = true; // Включаем режим чтения
                    try
                    {
                        // Обновляем X и Y, только если разница существенна (> 0.01px).
                        // Это фильтрует "шум" float-арифметики и предотвращает лишние обновления.
                        if (Math.Abs(GetX(control) - pos.Value.X) > 0.01)
                            SetX(control, pos.Value.X);

                        if (Math.Abs(GetY(control) - pos.Value.Y) > 0.01)
                            SetY(control, pos.Value.Y);
                    }
                    finally
                    {
                        _isUpdatingReadOnlyValues = false; // Выключаем режим чтения
                    }
                }
            }

            // 2. Всегда обновляем глобальные координаты рамки (на случай скролла, зума или ресайза родителя)
            UpdateDesignPosition(control);
        }
        finally
        {
            Interlocked.Exchange(ref _isInsidePositionChange, 0);
        }
    }

    /// <summary>
    /// Пересчитывает глобальные координаты (DesignX/DesignY) относительно DesignEditor.
    /// Использует приоритет Render для идеальной визуальной синхронизации рамки и элемента.
    /// </summary>
    private static void UpdateDesignPosition(Control control)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Interlocked.Exchange(ref _isInsidePositionChange, 1) == 1) return;
            try
            {
                // Пытаемся найти DesignEditor как точку отсчета.
                // Если его нет (например, превью или тесты), берем корень окна.
                Visual? reference = control.FindAncestorOfType<DesignEditor>() as Visual
                                    ?? control.GetVisualRoot() as Visual;

                if (reference != null)
                {
                    // Транслируем локальную точку (0,0) контрола в координаты Редактора
                    var position = control.TranslatePoint(new Point(0, 0), reference);

                    if (position.HasValue)
                    {
                        double currentDX = GetDesignX(control);
                        double currentDY = GetDesignY(control);

                        // Обновляем свойства, если позиция изменилась
                        if (Math.Abs(currentDX - position.Value.X) > 0.01)
                            SetDesignX(control, position.Value.X);

                        if (Math.Abs(currentDY - position.Value.Y) > 0.01)
                            SetDesignY(control, position.Value.Y);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isInsidePositionChange, 0);
            }
        }, DispatcherPriority.Render); // ВАЖНО: Render приоритет для синхронности с отрисовкой
    }

    // Хелпер: проверка, находимся ли мы прямо в Canvas
    private static bool IsInsideCanvas(Control control) => control.GetVisualParent() is Canvas;

    #region Accessors (Getters/Setters для удобства использования в C#)

    public static double GetX(AvaloniaObject obj) => obj.GetValue(XProperty);
    public static void SetX(AvaloniaObject obj, double value) => obj.SetValue(XProperty, value);

    public static double GetY(AvaloniaObject obj) => obj.GetValue(YProperty);
    public static void SetY(AvaloniaObject obj, double value) => obj.SetValue(YProperty, value);

    public static double GetDesignX(AvaloniaObject obj) => obj.GetValue(DesignXProperty);
    public static void SetDesignX(AvaloniaObject obj, double value) => obj.SetValue(DesignXProperty, value);

    public static double GetDesignY(AvaloniaObject obj) => obj.GetValue(DesignYProperty);
    public static void SetDesignY(AvaloniaObject obj, double value) => obj.SetValue(DesignYProperty, value);

    #endregion
}
