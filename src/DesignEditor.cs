using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace ArxisStudio;

/// <summary>
/// Визуальный редактор (Infinite Canvas).
/// Предоставляет бесконечную рабочую область с поддержкой масштабирования (Zoom),
/// панорамирования (Pan) и DPI-корректной сетки.
/// </summary>
public class DesignEditor : ContentControl
{
    #region Dependency Properties

    public static readonly StyledProperty<Point> ViewportLocationProperty =
        AvaloniaProperty.Register<DesignEditor, Point>(nameof(ViewportLocation));

    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<DesignEditor, double>(nameof(ViewportZoom), 1.0);

    public static readonly StyledProperty<Transform> ViewportTransformProperty =
        AvaloniaProperty.Register<DesignEditor, Transform>(nameof(ViewportTransform), new TransformGroup());

    public static readonly StyledProperty<Transform> DpiScaledViewportTransformProperty =
        AvaloniaProperty.Register<DesignEditor, Transform>(nameof(DpiScaledViewportTransform), new TransformGroup());

    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<DesignEditor, double>(nameof(MinZoom), 0.1);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<DesignEditor, double>(nameof(MaxZoom), 5.0);

    #endregion

    #region Property Wrappers

    public Point ViewportLocation
    {
        get => GetValue(ViewportLocationProperty);
        set => SetValue(ViewportLocationProperty, value);
    }

    public double ViewportZoom
    {
        get => GetValue(ViewportZoomProperty);
        set => SetValue(ViewportZoomProperty, value);
    }

    public Transform ViewportTransform
    {
        get => GetValue(ViewportTransformProperty);
        set => SetValue(ViewportTransformProperty, value);
    }

    public Transform DpiScaledViewportTransform
    {
        get => GetValue(DpiScaledViewportTransformProperty);
        set => SetValue(DpiScaledViewportTransformProperty, value);
    }

    public double MinZoom
    {
        get => GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    #endregion

    #region Internal Transforms

    private readonly TranslateTransform _translateTransform = new TranslateTransform();
    private readonly ScaleTransform _scaleTransform = new ScaleTransform();
    private readonly TranslateTransform _dpiTranslateTransform = new TranslateTransform();

    #endregion

    #region State

    private bool _isPanning;
    private Point _panStartMousePosition;
    private Point _panStartViewportLocation;

    #endregion

    static DesignEditor()
    {
        FocusableProperty.OverrideDefaultValue<DesignEditor>(true);

        // При изменении логических свойств обновляем матрицы
        ViewportLocationProperty.Changed.AddClassHandler<DesignEditor>((x, e) => x.UpdateTransforms());
        ViewportZoomProperty.Changed.AddClassHandler<DesignEditor>((x, e) => x.UpdateTransforms());
    }

    public DesignEditor()
    {
        // Инициализация групп трансформаций, чтобы избежать null до первого UpdateTransforms
        var contentGroup = new TransformGroup();
        contentGroup.Children.Add(_scaleTransform);
        contentGroup.Children.Add(_translateTransform);
        ViewportTransform = contentGroup;

        var dpiGroup = new TransformGroup();
        dpiGroup.Children.Add(_scaleTransform);
        dpiGroup.Children.Add(_dpiTranslateTransform);
        DpiScaledViewportTransform = dpiGroup;
    }

    #region Lifecycle & DPI Handling

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // 1. Подписываемся на изменение Scaling (смена монитора / изменение DPI системы)
        if (e.Root is TopLevel topLevel)
        {
            topLevel.ScalingChanged += OnScreenScalingChanged;
        }

        // 2. Критически важно: вызываем обновление сразу после присоединения.
        // В этот момент e.Root уже доступен, и мы можем получить реальный RenderScaling.
        UpdateTransforms();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Отписываемся, чтобы избежать утечек памяти
        if (e.Root is TopLevel topLevel)
        {
            topLevel.ScalingChanged -= OnScreenScalingChanged;
        }
    }

    private void OnScreenScalingChanged(object? sender, EventArgs e)
    {
        // При изменении DPI пересчитываем "Snapping" для сетки
        UpdateTransforms();
    }

    #endregion

    #region Transform Logic

    private void UpdateTransforms()
    {
        // 1. Обновляем Масштаб (общий)
        _scaleTransform.ScaleX = ViewportZoom;
        _scaleTransform.ScaleY = ViewportZoom;

        // 2. Вычисляем базовое смещение: Offset = -Location * Zoom
        double x = -ViewportLocation.X * ViewportZoom;
        double y = -ViewportLocation.Y * ViewportZoom;

        // 3. Обновляем смещение контента (плавное, float coordinates)
        _translateTransform.X = x;
        _translateTransform.Y = y;

        // 4. Обновляем смещение Сетки (Pixel Snapping)
        var root = this.GetVisualRoot();
        double renderScaling = root?.RenderScaling ?? 1.0;

        // Округляем координаты до ближайшего физического пикселя устройства.
        // Это предотвращает размытие линий DrawingBrush.
        _dpiTranslateTransform.X = Math.Round(x * renderScaling) / renderScaling;
        _dpiTranslateTransform.Y = Math.Round(y * renderScaling) / renderScaling;

        // 5. Пересоздаем TransformGroup.
        // Это необходимо для "грязного" обновления биндингов в Avalonia/WPF,
        // чтобы визуальное дерево (особенно DrawingBrush) точно перерисовалось.

        var viewportGroup = new TransformGroup();
        viewportGroup.Children.Add(_scaleTransform);
        viewportGroup.Children.Add(_translateTransform);
        SetCurrentValue(ViewportTransformProperty, viewportGroup);

        var dpiGroup = new TransformGroup();
        dpiGroup.Children.Add(_scaleTransform);
        dpiGroup.Children.Add(_dpiTranslateTransform);
        SetCurrentValue(DpiScaledViewportTransformProperty, dpiGroup);
    }

    #endregion

    #region Input Handling

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.Handled) return;

        double zoomFactor = 1.1;
        double prevZoom = ViewportZoom;

        // Zoom In или Out
        double newZoom = e.Delta.Y > 0 ? prevZoom * zoomFactor : prevZoom / zoomFactor;
        newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));

        if (Math.Abs(newZoom - prevZoom) > 0.0001)
        {
            Point mousePos = e.GetPosition(this); // Позиция мыши относительно Viewport

            // Математика зума "в точку курсора":
            // Старая позиция в мире - Новая позиция в мире = Смещение камеры
            Vector correction = (Vector)mousePos / prevZoom - (Vector)mousePos / newZoom;

            ViewportZoom = newZoom;
            ViewportLocation += correction;
        }

        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        bool isAltPressed = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        bool isMiddleButton = props.IsMiddleButtonPressed;

        // Проверка: Если клик был по интерактивному элементу внутри (кнопка, текстбокс),
        // и мы НЕ держим Alt, то не перехватываем событие.
        if (!isAltPressed && !isMiddleButton)
        {
            if (e.Source is Control source && source != this)
            {
                // Ищем, не является ли источник частью шаблона самого редактора (Canvas)
                if (source.Name != "PART_Canvas" && source.Name != "PART_ContentPresenter")
                {
                    // Если это какой-то вложенный контрол - даем ему обработать клик.
                    base.OnPointerPressed(e);
                    return;
                }
            }
        }

        // Если нажата СКМ или Alt+ЛКМ -> начинаем Pan
        if (isMiddleButton || (props.IsLeftButtonPressed && isAltPressed))
        {
            _isPanning = true;
            _panStartMousePosition = e.GetPosition(this); // Экранные координаты
            _panStartViewportLocation = ViewportLocation; // Логические координаты

            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
        else
        {
            base.OnPointerPressed(e);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isPanning)
        {
            base.OnPointerMoved(e);
            return;
        }

        Point currentMousePos = e.GetPosition(this);
        Vector diffScreen = _panStartMousePosition - currentMousePos;

        // Конвертируем дельту экрана в дельту мира (учитывая зум)
        ViewportLocation = _panStartViewportLocation + (diffScreen / ViewportZoom);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            Cursor = Cursor.Default;
        }
        base.OnPointerReleased(e);
    }

    #endregion
}
