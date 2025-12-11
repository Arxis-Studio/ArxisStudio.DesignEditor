using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Selection;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ArxisStudio;

public class DesignEditor : SelectingItemsControl
{
    #region Constants
    private const double ZoomFactor = 1.1;
    private const double ZoomTolerance = 0.0001;
    #endregion

    #region Re-exposed Properties

    // --- ИСПРАВЛЕНИЕ: Открываем доступ к Selection для состояний ---
    public new ISelectionModel Selection
    {
        get => base.Selection;
        set => base.Selection = value;
    }

    public new static readonly DirectProperty<DesignEditor, IList?> SelectedItemsProperty =
        AvaloniaProperty.RegisterDirect<DesignEditor, IList?>(nameof(SelectedItems), o => o.SelectedItems, (o, v) => o.SelectedItems = v);

    public new IList? SelectedItems
    {
        get => base.SelectedItems;
        set => base.SelectedItems = value;
    }

    public new SelectionMode SelectionMode
    {
        get => base.SelectionMode;
        set => base.SelectionMode = value;
    }
    #endregion

    #region Dependency Properties
    public static readonly StyledProperty<Point> ViewportLocationProperty = AvaloniaProperty.Register<DesignEditor, Point>(nameof(ViewportLocation));
    public static readonly StyledProperty<double> ViewportZoomProperty = AvaloniaProperty.Register<DesignEditor, double>(nameof(ViewportZoom), 1.0);
    public static readonly StyledProperty<double> MinZoomProperty = AvaloniaProperty.Register<DesignEditor, double>(nameof(MinZoom), 0.1);
    public static readonly StyledProperty<double> MaxZoomProperty = AvaloniaProperty.Register<DesignEditor, double>(nameof(MaxZoom), 5.0);

    public static readonly StyledProperty<Transform> ViewportTransformProperty = AvaloniaProperty.Register<DesignEditor, Transform>(nameof(ViewportTransform), new TransformGroup());
    public static readonly StyledProperty<Transform> DpiScaledViewportTransformProperty = AvaloniaProperty.Register<DesignEditor, Transform>(nameof(DpiScaledViewportTransform), new TransformGroup());

    public static readonly StyledProperty<ControlTheme> SelectionRectangleStyleProperty = AvaloniaProperty.Register<DesignEditor, ControlTheme>(nameof(SelectionRectangleStyle));
    public static readonly DirectProperty<DesignEditor, bool> IsSelectingProperty = AvaloniaProperty.RegisterDirect<DesignEditor, bool>(nameof(IsSelecting), o => o.IsSelecting);
    public static readonly DirectProperty<DesignEditor, Rect> SelectedAreaProperty = AvaloniaProperty.RegisterDirect<DesignEditor, Rect>(nameof(SelectedArea), o => o.SelectedArea);

    public static readonly DirectProperty<DesignEditor, Rect> ItemsExtentProperty = AvaloniaProperty.RegisterDirect<DesignEditor, Rect>(nameof(ItemsExtent), o => o.ItemsExtent);
    #endregion

    #region Wrappers
    public Point ViewportLocation { get => GetValue(ViewportLocationProperty); set => SetValue(ViewportLocationProperty, value); }
    public double ViewportZoom { get => GetValue(ViewportZoomProperty); set => SetValue(ViewportZoomProperty, value); }
    public double MinZoom { get => GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }
    public double MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }
    public Transform ViewportTransform { get => GetValue(ViewportTransformProperty); set => SetValue(ViewportTransformProperty, value); }
    public Transform DpiScaledViewportTransform { get => GetValue(DpiScaledViewportTransformProperty); set => SetValue(DpiScaledViewportTransformProperty, value); }
    public ControlTheme SelectionRectangleStyle { get => GetValue(SelectionRectangleStyleProperty); set => SetValue(SelectionRectangleStyleProperty, value); }

    private bool _isSelecting;
    public bool IsSelecting { get => _isSelecting; private set => SetAndRaise(IsSelectingProperty, ref _isSelecting, value); }
    private Rect _selectedArea;
    public Rect SelectedArea { get => _selectedArea; private set => SetAndRaise(SelectedAreaProperty, ref _selectedArea, value); }
    private Rect _itemsExtent;
    public Rect ItemsExtent { get => _itemsExtent; set => SetAndRaise(ItemsExtentProperty, ref _itemsExtent, value); }
    #endregion

    #region Internal State
    private readonly TranslateTransform _translateTransform = new TranslateTransform();
    private readonly ScaleTransform _scaleTransform = new ScaleTransform();
    private readonly TranslateTransform _dpiTranslateTransform = new TranslateTransform();
    private bool _isPanning;
    private Point _panStartMousePosition;
    private Point _panStartViewportLocation;
    private Point _selectionStartLocationWorld;
    #endregion

    static DesignEditor()
    {
        FocusableProperty.OverrideDefaultValue<DesignEditor>(true);
        ViewportLocationProperty.Changed.AddClassHandler<DesignEditor>((x, e) => x.UpdateTransforms());
        ViewportZoomProperty.Changed.AddClassHandler<DesignEditor>((x, e) => x.UpdateTransforms());

        // Подписка на события перемещения элементов
        DesignEditorItem.DragStartedEvent.AddClassHandler<DesignEditor>((x, e) => x.OnItemsDragStarted(e));
        DesignEditorItem.DragDeltaEvent.AddClassHandler<DesignEditor>((x, e) => x.OnItemsDragDelta(e));
        DesignEditorItem.DragCompletedEvent.AddClassHandler<DesignEditor>((x, e) => x.OnItemsDragCompleted(e));
    }

    public DesignEditor()
    {
        SelectionMode = SelectionMode.Multiple;

        var contentGroup = new TransformGroup();
        contentGroup.Children.Add(_scaleTransform);
        contentGroup.Children.Add(_translateTransform);
        SetCurrentValue(ViewportTransformProperty, contentGroup);

        var dpiGroup = new TransformGroup();
        dpiGroup.Children.Add(_scaleTransform);
        dpiGroup.Children.Add(_dpiTranslateTransform);
        SetCurrentValue(DpiScaledViewportTransformProperty, dpiGroup);
    }

    // --- Container Logic ---
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        return NeedsContainer<DesignEditorItem>(item, out recycleKey);
    }

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        return new DesignEditorItem();
    }

    // --- Lifecycle ---
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (e.Root is TopLevel topLevel) topLevel.ScalingChanged += OnScreenScalingChanged;
        UpdateTransforms();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (e.Root is TopLevel topLevel) topLevel.ScalingChanged -= OnScreenScalingChanged;
    }

    private void OnScreenScalingChanged(object? sender, EventArgs e) => UpdateTransforms();

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

        var vg = new TransformGroup(); vg.Children.Add(_scaleTransform); vg.Children.Add(_translateTransform);
        SetCurrentValue(ViewportTransformProperty, vg);

        var dg = new TransformGroup(); dg.Children.Add(_scaleTransform); dg.Children.Add(_dpiTranslateTransform);
        SetCurrentValue(DpiScaledViewportTransformProperty, dg);
    }

    private Point GetWorldPosition(Point screenPoint)
        => (screenPoint / ViewportZoom) + ViewportLocation;

    // --- Drag & Drop Logic ---

    private void OnItemsDragStarted(DragStartedEventArgs e)
    {
        e.Handled = true;
    }

    private void OnItemsDragDelta(DragDeltaEventArgs e)
    {
        if (_isPanning || IsSelecting) return;

        var items = SelectedItems;
        if (items == null || items.Count == 0) return;

        var delta = new Vector(e.HorizontalChange, e.VerticalChange);

        foreach (var item in items)
        {
            var container = ContainerFromItem(item) as DesignEditorItem;
            if (container == null && item is DesignEditorItem directItem)
                container = directItem;

            if (container != null && container.IsDraggable)
            {
                container.Location += delta;
            }
        }
        e.Handled = true;
    }

    private void OnItemsDragCompleted(DragCompletedEventArgs e)
    {
        e.Handled = true;
    }

    // --- Input Handling ---
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (e.Handled) return;
        double prevZoom = ViewportZoom;
        double newZoom = e.Delta.Y > 0 ? prevZoom * ZoomFactor : prevZoom / ZoomFactor;

        // Исправление для .NET Standard 2.0 (Math.Clamp недоступен)
        newZoom = Math.Max(GetValue(MinZoomProperty), Math.Min(GetValue(MaxZoomProperty), newZoom));

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
        var source = e.Source as Visual;
        var itemContainer = source?.FindAncestorOfType<DesignEditorItem>();

        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            _isPanning = true;
            _panStartMousePosition = e.GetPosition(this);
            _panStartViewportLocation = ViewportLocation;
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
        else if (props.IsLeftButtonPressed && itemContainer != null)
        {
            base.OnPointerPressed(e);
        }
        else if (props.IsLeftButtonPressed)
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) SelectedItem = null;
            IsSelecting = true;
            _selectionStartLocationWorld = GetWorldPosition(e.GetPosition(this));

            // Исправление Size.Empty для Avalonia
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
            CommitSelection(SelectedArea, e.KeyModifiers.HasFlag(KeyModifiers.Control));
            IsSelecting = false;
            SelectedArea = new Rect(0, 0, 0, 0);
            e.Pointer.Capture(null);
        }
        base.OnPointerReleased(e);
    }

    private void CommitSelection(Rect bounds, bool isCtrlPressed)
    {
        if (Presenter?.Panel == null) return;

        using (Selection.BatchUpdate())
        {
            if (!isCtrlPressed) Selection.Clear();

            foreach (var child in Presenter.Panel.Children)
            {
                if (child is DesignEditorItem container)
                {
                    if (bounds.Intersects(new Rect(container.Location, container.Bounds.Size)))
                        Selection.Select(IndexFromContainer(container));
                }
            }
        }
    }
}
