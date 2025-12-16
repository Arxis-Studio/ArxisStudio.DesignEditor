using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ArxisStudio.States;
using ArxisStudio.Controls;
using ArxisStudio.Attached;

namespace ArxisStudio;

/// <summary>
/// Контейнер для элемента дизайнера.
/// <para>
/// Поддерживает перетаскивание, выделение, изменение размеров и синхронизацию
/// свойства Location с системой Layout.
/// </para>
/// </summary>
[TemplatePart("PART_Border", typeof(Border))]
[TemplatePart("PART_Resizer", typeof(ResizeAdorner))]
[PseudoClasses(":selected", ":dragging", ":resizing")]
public class DesignEditorItem : ContentControl, ISelectable, IDesignEditorItem
{
    #region Fields
    private ResizeAdorner? _resizeAdorner;
    private readonly Stack<DesignEditorItemState> _states = new();
    #endregion

    #region Properties

    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<DesignEditorItem>();

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public static readonly StyledProperty<Point> LocationProperty =
        AvaloniaProperty.Register<DesignEditorItem, Point>(nameof(Location));

    /// <summary>
    /// Логическая позиция элемента.
    /// При изменении обновляет <see cref="Layout.XProperty"/> и <see cref="Layout.YProperty"/>.
    /// </summary>
    public Point Location
    {
        get => GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    public static readonly StyledProperty<bool> IsDraggableProperty =
        AvaloniaProperty.Register<DesignEditorItem, bool>(nameof(IsDraggable), true);

    public bool IsDraggable
    {
        get => GetValue(IsDraggableProperty);
        set => SetValue(IsDraggableProperty, value);
    }

    public static readonly StyledProperty<IBrush> SelectedBrushProperty =
        AvaloniaProperty.Register<DesignEditorItem, IBrush>(nameof(SelectedBrush), Brushes.Orange);

    public IBrush SelectedBrush
    {
        get => GetValue(SelectedBrushProperty);
        set => SetValue(SelectedBrushProperty, value);
    }

    public static readonly StyledProperty<Thickness> SelectedBorderThicknessProperty =
        AvaloniaProperty.Register<DesignEditorItem, Thickness>(nameof(SelectedBorderThickness), new Thickness(2));

    public Thickness SelectedBorderThickness
    {
        get => GetValue(SelectedBorderThicknessProperty);
        set => SetValue(SelectedBorderThicknessProperty, value);
    }

    public static readonly DirectProperty<DesignEditorItem, Thickness> SelectedMarginProperty =
        AvaloniaProperty.RegisterDirect<DesignEditorItem, Thickness>(
            nameof(SelectedMargin),
            o => o.SelectedMargin);

    public Thickness SelectedMargin => new Thickness(
        BorderThickness.Left - SelectedBorderThickness.Left,
        BorderThickness.Top - SelectedBorderThickness.Top,
        BorderThickness.Right - SelectedBorderThickness.Right,
        BorderThickness.Bottom - SelectedBorderThickness.Bottom);

    #endregion

    #region Events

    public static readonly RoutedEvent<DragStartedEventArgs> DragStartedEvent =
        RoutedEvent.Register<DragStartedEventArgs>(nameof(DragStarted), RoutingStrategies.Bubble, typeof(DesignEditorItem));
    public static readonly RoutedEvent<DragDeltaEventArgs> DragDeltaEvent =
        RoutedEvent.Register<DragDeltaEventArgs>(nameof(DragDelta), RoutingStrategies.Bubble, typeof(DesignEditorItem));
    public static readonly RoutedEvent<DragCompletedEventArgs> DragCompletedEvent =
        RoutedEvent.Register<DragCompletedEventArgs>(nameof(DragCompleted), RoutingStrategies.Bubble, typeof(DesignEditorItem));

    public static readonly RoutedEvent<ResizeDeltaEventArgs> ResizeDeltaEvent =
        RoutedEvent.Register<ResizeDeltaEventArgs>(nameof(ResizeDelta), RoutingStrategies.Bubble, typeof(DesignEditorItem));
    public static readonly RoutedEvent<VectorEventArgs> ResizeStartedEvent =
        RoutedEvent.Register<VectorEventArgs>(nameof(ResizeStarted), RoutingStrategies.Bubble, typeof(DesignEditorItem));
    public static readonly RoutedEvent<VectorEventArgs> ResizeCompletedEvent =
        RoutedEvent.Register<VectorEventArgs>(nameof(ResizeCompleted), RoutingStrategies.Bubble, typeof(DesignEditorItem));

    public event EventHandler<DragStartedEventArgs> DragStarted { add => AddHandler(DragStartedEvent, value); remove => RemoveHandler(DragStartedEvent, value); }
    public event EventHandler<DragDeltaEventArgs> DragDelta { add => AddHandler(DragDeltaEvent, value); remove => RemoveHandler(DragDeltaEvent, value); }
    public event EventHandler<DragCompletedEventArgs> DragCompleted { add => AddHandler(DragCompletedEvent, value); remove => RemoveHandler(DragCompletedEvent, value); }
    public event EventHandler<ResizeDeltaEventArgs> ResizeDelta { add => AddHandler(ResizeDeltaEvent, value); remove => RemoveHandler(ResizeDeltaEvent, value); }
    public event EventHandler<VectorEventArgs> ResizeStarted { add => AddHandler(ResizeStartedEvent, value); remove => RemoveHandler(ResizeStartedEvent, value); }
    public event EventHandler<VectorEventArgs> ResizeCompleted { add => AddHandler(ResizeCompletedEvent, value); remove => RemoveHandler(ResizeCompletedEvent, value); }

    #endregion

    /// <summary>
    /// Текущее состояние машины состояний.
    /// </summary>
    public DesignEditorItemState CurrentState => _states.Count > 0 ? _states.Peek() : null!;

    static DesignEditorItem()
    {
        SelectableMixin.Attach<DesignEditorItem>(IsSelectedProperty);
        FocusableProperty.OverrideDefaultValue<DesignEditorItem>(true);
    }

    public DesignEditorItem()
    {
        _states.Push(new ItemIdleState(this));
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_resizeAdorner != null)
        {
            _resizeAdorner.ResizeDelta -= OnAdornerResizeDelta;
            _resizeAdorner.ResizeStarted -= OnAdornerResizeStarted;
            _resizeAdorner.ResizeCompleted -= OnAdornerResizeCompleted;
        }

        _resizeAdorner = e.NameScope.Find<ResizeAdorner>("PART_Resizer");

        if (_resizeAdorner != null)
        {
            _resizeAdorner.ResizeDelta += OnAdornerResizeDelta;
            _resizeAdorner.ResizeStarted += OnAdornerResizeStarted;
            _resizeAdorner.ResizeCompleted += OnAdornerResizeCompleted;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BorderThicknessProperty ||
            change.Property == SelectedBorderThicknessProperty)
        {
            RaisePropertyChanged(SelectedMarginProperty, default, SelectedMargin);
        }
        else if (change.Property == IsSelectedProperty)
        {
            UpdatePseudoClasses();
        }
        else if (change.Property == LocationProperty)
        {
            Layout.SetX(this, Location.X);
            Layout.SetY(this, Location.Y);
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":selected", IsSelected);
    }

    #region State Machine

    public void PushState(DesignEditorItemState state)
    {
        var previous = CurrentState;
        _states.Push(state);
        state.Enter(previous);
        UpdatePseudoClassesState(state);
    }

    public void PopState()
    {
        if (_states.Count > 1)
        {
            var current = _states.Pop();
            current.Exit();
            CurrentState.ReEnter(current);
            UpdatePseudoClassesState(CurrentState);
        }
    }

    private void UpdatePseudoClassesState(DesignEditorItemState state)
    {
        PseudoClasses.Set(":dragging", state is ItemDraggingState);
        PseudoClasses.Set(":resizing", state is ItemResizingState);
    }

    #endregion

    // Обработчики ввода
    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); if (!e.Handled) CurrentState.OnPointerPressed(e); }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); CurrentState.OnPointerMoved(e); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); CurrentState.OnPointerReleased(e); }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); while (_states.Count > 1) PopState(); }

    // Обработчики адорнера
    private void OnAdornerResizeStarted(object? sender, ResizeStartedEventArgs e) { PushState(new ItemResizingState(this, e.Direction)); RaiseEvent(new VectorEventArgs { RoutedEvent = ResizeStartedEvent, Vector = e.Vector }); e.Handled = true; }
    private void OnAdornerResizeDelta(object? sender, ResizeDeltaEventArgs e) { CurrentState.OnResizeDelta(e); e.Handled = true; }
    private void OnAdornerResizeCompleted(object? sender, VectorEventArgs e) { PopState(); RaiseEvent(new VectorEventArgs { RoutedEvent = ResizeCompletedEvent, Vector = e.Vector }); e.Handled = true; }

    internal void OnDragStarted(DragStartedEventArgs e) => RaiseEvent(e);
    internal void OnDragDelta(DragDeltaEventArgs e) => RaiseEvent(e);
    internal void OnDragCompleted(DragCompletedEventArgs e) => RaiseEvent(e);
}
