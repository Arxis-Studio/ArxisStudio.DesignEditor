
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ArxisStudio;

/// <summary>
/// Визуальный редактор (Infinite Canvas).
/// Поддерживает: Zoom, Pan, DPI-сетку, Rubberband Selection.
/// </summary>
public class DesignEditor : ContentControl
{
    #region Constants

    private const double ZoomFactor = 1.1;
    private const double ZoomTolerance = 0.0001;

    #endregion

    #region Dependency Properties: Viewport

    public static readonly StyledProperty<Point> ViewportLocationProperty =
        AvaloniaProperty.Register<DesignEditor, Point>(nameof(ViewportLocation));

    public static readonly StyledProperty<double> ViewportZoomProperty =
        AvaloniaProperty.Register<DesignEditor, double>(nameof(ViewportZoom), 1.0);

    public static readonly StyledProperty<double> MinZoomProperty =
        AvaloniaProperty.Register<DesignEditor, double>(nameof(MinZoom), 0.1);

    public static readonly StyledProperty<double> MaxZoomProperty =
        AvaloniaProperty.Register<DesignEditor, double>(nameof(MaxZoom), 5.0);

    #endregion

    #region Dependency Properties: Transforms & Styles

    public static readonly StyledProperty<Transform> ViewportTransformProperty =
        AvaloniaProperty.Register<DesignEditor, Transform>(nameof(ViewportTransform), new TransformGroup());

    public static readonly StyledProperty<Transform> DpiScaledViewportTransformProperty =
        AvaloniaProperty.Register<DesignEditor, Transform>(nameof(DpiScaledViewportTransform), new TransformGroup());

    public static readonly StyledProperty<ControlTheme> SelectionRectangleStyleProperty =
        AvaloniaProperty.Register<DesignEditor, ControlTheme>(nameof(SelectionRectangleStyle));

    #endregion

    #region Dependency Properties: Selection State

    public static readonly DirectProperty<DesignEditor, bool> IsSelectingProperty =
        AvaloniaProperty.RegisterDirect<DesignEditor, bool>(nameof(IsSelecting), o => o.IsSelecting);

    public static readonly DirectProperty<DesignEditor, Rect> SelectedAreaProperty =
        AvaloniaProperty.RegisterDirect<DesignEditor, Rect>(nameof(SelectedArea), o => o.SelectedArea);

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

    public ControlTheme SelectionRectangleStyle
    {
        get => GetValue(SelectionRectangleStyleProperty);
        set => SetValue(SelectionRectangleStyleProperty, value);
    }

    private bool _isSelecting;
    /// <summary>
    /// Показывает, активно ли сейчас выделение рамкой.
    /// </summary>
    public bool IsSelecting
    {
        get => _isSelecting;
        private set => SetAndRaise(IsSelectingProperty, ref _isSelecting, value);
    }

    private Rect _selectedArea;
    /// <summary>
    /// Координаты текущей рамки выделения (в мировых координатах).
    /// </summary>
    public Rect SelectedArea
    {
        get => _selectedArea;
        private set => SetAndRaise(SelectedAreaProperty, ref _selectedArea, value);
    }

    #endregion

    #region Internal Transforms & State

    private readonly TranslateTransform _translateTransform = new TranslateTransform();
    private readonly ScaleTransform _scaleTransform = new ScaleTransform();
    private readonly TranslateTransform _dpiTranslateTransform = new TranslateTransform();

    private bool _isPanning;
    private Point _panStartMousePosition;
    private Point _panStartViewportLocation;
    private Point _selectionStartLocationWorld;

    #endregion

    #region Lifecycle

    static DesignEditor()
    {
        FocusableProperty.OverrideDefaultValue<DesignEditor>(true);

        ViewportLocationProperty.Changed.AddClassHandler<DesignEditor>((x, e) => x.UpdateTransforms());
        ViewportZoomProperty.Changed.AddClassHandler<DesignEditor>((x, e) => x.UpdateTransforms());
    }

    public DesignEditor()
    {
        var contentGroup = new TransformGroup();
        contentGroup.Children.Add(_scaleTransform);
        contentGroup.Children.Add(_translateTransform);
        ViewportTransform = contentGroup;

        var dpiGroup = new TransformGroup();
        dpiGroup.Children.Add(_scaleTransform);
        dpiGroup.Children.Add(_dpiTranslateTransform);
        DpiScaledViewportTransform = dpiGroup;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Подписка на изменение DPI для корректного Snapping сетки
        if (e.Root is TopLevel topLevel)
        {
            topLevel.ScalingChanged += OnScreenScalingChanged;
        }
        UpdateTransforms();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (e.Root is TopLevel topLevel)
        {
            topLevel.ScalingChanged -= OnScreenScalingChanged;
        }
    }

    private void OnScreenScalingChanged(object? sender, EventArgs e) => UpdateTransforms();

    #endregion

    #region Core Logic

    private void UpdateTransforms()
    {
        _scaleTransform.ScaleX = ViewportZoom;
        _scaleTransform.ScaleY = ViewportZoom;

        double x = -ViewportLocation.X * ViewportZoom;
        double y = -ViewportLocation.Y * ViewportZoom;

        _translateTransform.X = x;
        _translateTransform.Y = y;

        var root = this.GetVisualRoot();
        double renderScaling = root?.RenderScaling ?? 1.0;

        _dpiTranslateTransform.X = Math.Round(x * renderScaling) / renderScaling;
        _dpiTranslateTransform.Y = Math.Round(y * renderScaling) / renderScaling;

        // Важно пересоздавать группы для триггера обновления в UI
        var viewportGroup = new TransformGroup();
        viewportGroup.Children.Add(_scaleTransform);
        viewportGroup.Children.Add(_translateTransform);
        SetCurrentValue(ViewportTransformProperty, viewportGroup);

        var dpiGroup = new TransformGroup();
        dpiGroup.Children.Add(_scaleTransform);
        dpiGroup.Children.Add(_dpiTranslateTransform);
        SetCurrentValue(DpiScaledViewportTransformProperty, dpiGroup);
    }

    private Point GetWorldPosition(Point screenPoint)
    {
        // World = (Screen / Zoom) + Offset
        return (screenPoint / ViewportZoom) + ViewportLocation;
    }

    #endregion

    #region Input Handling

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.Handled) return;

        double prevZoom = ViewportZoom;
        double newZoom = e.Delta.Y > 0 ? prevZoom * ZoomFactor : prevZoom / ZoomFactor;

        // .NET Standard 2.0 compatible clamp
        newZoom = Math.Max(MinZoom, Math.Min(MaxZoom, newZoom));

        if (Math.Abs(newZoom - prevZoom) > ZoomTolerance)
        {
            Point mousePos = e.GetPosition(this);
            Vector correction = (Vector)mousePos / prevZoom - (Vector)mousePos / newZoom;

            ViewportZoom = newZoom;
            ViewportLocation += correction;
        }

        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        // 1. Проверяем, кликнули ли мы по интерактивному элементу внутри (кнопка, текстбокс)
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt) && !props.IsMiddleButtonPressed)
        {
            if (e.Source is Control source)
            {
                // Если элемент не является частью шаблона редактора (как Canvas или Border),
                // то это контент пользователя.
                if (source.TemplatedParent != this && source != this)
                {
                    base.OnPointerPressed(e);
                    return;
                }
            }
        }

        // 2. Pan (Панорамирование): СКМ или Alt+ЛКМ
        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _isPanning = true;
            _panStartMousePosition = e.GetPosition(this);
            _panStartViewportLocation = ViewportLocation;

            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
        // 3. Selection (Выделение): ЛКМ
        else if (props.IsLeftButtonPressed)
        {
            IsSelecting = true;

            _selectionStartLocationWorld = GetWorldPosition(e.GetPosition(this));

            // Начинаем с нулевого размера
            SelectedArea = new Rect(_selectionStartLocationWorld, new Size(0, 0));

            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else
        {
            base.OnPointerPressed(e);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_isPanning)
        {
            Vector diffScreen = _panStartMousePosition - e.GetPosition(this);
            ViewportLocation = _panStartViewportLocation + (diffScreen / ViewportZoom);
        }
        else if (IsSelecting)
        {
            Point currentMousePosWorld = GetWorldPosition(e.GetPosition(this));

            // Логика разнонаправленного выделения (влево-вправо, вверх-вниз)
            double x = Math.Min(_selectionStartLocationWorld.X, currentMousePosWorld.X);
            double y = Math.Min(_selectionStartLocationWorld.Y, currentMousePosWorld.Y);
            double w = Math.Abs(_selectionStartLocationWorld.X - currentMousePosWorld.X);
            double h = Math.Abs(_selectionStartLocationWorld.Y - currentMousePosWorld.Y);

            SelectedArea = new Rect(x, y, w, h);
        }
        else
        {
            base.OnPointerMoved(e);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            Cursor = Cursor.Default;
            e.Pointer.Capture(null);
        }
        else if (IsSelecting)
        {
            IsSelecting = false; // Скрываем рамку
            SelectedArea = new Rect(0, 0, 0, 0); // Сбрасываем координаты

            e.Pointer.Capture(null);

            // TODO: Здесь можно добавить логику "SelectItemsInArea(SelectedArea)"
        }

        base.OnPointerReleased(e);
    }

    #endregion
}
