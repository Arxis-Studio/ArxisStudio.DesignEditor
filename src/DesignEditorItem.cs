using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ArxisStudio.States;
using Avalonia.Controls.Primitives;

namespace ArxisStudio;

/// <summary>
/// Контейнер для элемента дизайнера.
/// Поддерживает перетаскивание, выделение, виртуализацию координат и стилизацию в стиле Nodify.
/// </summary>
public class DesignEditorItem : ContentControl, ISelectable, IDesignEditorItem
{
    #region Standard Properties

    /// <summary>
    /// Определяет, выбран ли данный элемент.
    /// </summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        SelectingItemsControl.IsSelectedProperty.AddOwner<DesignEditorItem>();

    /// <summary>
    /// Возвращает или задает состояние выбора элемента.
    /// </summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Свойство зависимости для <see cref="Location"/>.
    /// </summary>
    public static readonly StyledProperty<Point> LocationProperty =
        AvaloniaProperty.Register<DesignEditorItem, Point>(nameof(Location));

    /// <summary>
    /// Координаты элемента на холсте (в логических единицах).
    /// </summary>
    public Point Location
    {
        get => GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    /// <summary>
    /// Свойство зависимости для <see cref="IsDraggable"/>.
    /// </summary>
    public static readonly StyledProperty<bool> IsDraggableProperty =
        AvaloniaProperty.Register<DesignEditorItem, bool>(nameof(IsDraggable), true);

    /// <summary>
    /// Разрешено ли перетаскивание данного элемента мышью.
    /// </summary>
    public bool IsDraggable
    {
        get => GetValue(IsDraggableProperty);
        set => SetValue(IsDraggableProperty, value);
    }

    #endregion

    #region Visual Properties (Nodify Style)

    /// <summary>
    /// Свойство зависимости для <see cref="SelectedBrush"/>.
    /// </summary>
    public static readonly StyledProperty<IBrush> SelectedBrushProperty =
        AvaloniaProperty.Register<DesignEditorItem, IBrush>(nameof(SelectedBrush), Brushes.Orange);

    /// <summary>
    /// Кисть границы при выделенном состоянии (по умолчанию Orange, как в Nodify).
    /// </summary>
    public IBrush SelectedBrush
    {
        get => GetValue(SelectedBrushProperty);
        set => SetValue(SelectedBrushProperty, value);
    }

    /// <summary>
    /// Свойство зависимости для <see cref="SelectedBorderThickness"/>.
    /// </summary>
    public static readonly StyledProperty<Thickness> SelectedBorderThicknessProperty =
        AvaloniaProperty.Register<DesignEditorItem, Thickness>(nameof(SelectedBorderThickness), new Thickness(2));

    /// <summary>
    /// Толщина границы при выделенном состоянии.
    /// </summary>
    public Thickness SelectedBorderThickness
    {
        get => GetValue(SelectedBorderThicknessProperty);
        set => SetValue(SelectedBorderThicknessProperty, value);
    }

    /// <summary>
    /// Прямое свойство Avalonia для <see cref="SelectedMargin"/>.
    /// </summary>
    public static readonly DirectProperty<DesignEditorItem, Thickness> SelectedMarginProperty =
        AvaloniaProperty.RegisterDirect<DesignEditorItem, Thickness>(
            nameof(SelectedMargin),
            o => o.SelectedMargin);

    /// <summary>
    /// Вычисляемый отступ для компенсации утолщения рамки при выделении.
    /// Позволяет рамке расти "наружу", не сдвигая контент.
    /// Значение рассчитывается как (NormalThickness - SelectedThickness).
    /// </summary>
    public Thickness SelectedMargin => new Thickness(
        BorderThickness.Left - SelectedBorderThickness.Left,
        BorderThickness.Top - SelectedBorderThickness.Top,
        BorderThickness.Right - SelectedBorderThickness.Right,
        BorderThickness.Bottom - SelectedBorderThickness.Bottom);

    #endregion

    #region Routed Events

    /// <summary>
    /// Событие, возникающее при начале перетаскивания элемента.
    /// </summary>
    public static readonly RoutedEvent<DragStartedEventArgs> DragStartedEvent =
        RoutedEvent.Register<DragStartedEventArgs>(nameof(DragStarted), RoutingStrategies.Bubble, typeof(DesignEditorItem));

    /// <summary>
    /// Событие, возникающее при перемещении элемента (delta).
    /// </summary>
    public static readonly RoutedEvent<DragDeltaEventArgs> DragDeltaEvent =
        RoutedEvent.Register<DragDeltaEventArgs>(nameof(DragDelta), RoutingStrategies.Bubble, typeof(DesignEditorItem));

    /// <summary>
    /// Событие, возникающее при завершении перетаскивания.
    /// </summary>
    public static readonly RoutedEvent<DragCompletedEventArgs> DragCompletedEvent =
        RoutedEvent.Register<DragCompletedEventArgs>(nameof(DragCompleted), RoutingStrategies.Bubble, typeof(DesignEditorItem));

    /// <summary>
    /// Подписка на событие начала перетаскивания.
    /// </summary>
    public event EventHandler<DragStartedEventArgs> DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    /// <summary>
    /// Подписка на событие процесса перетаскивания.
    /// </summary>
    public event EventHandler<DragDeltaEventArgs> DragDelta
    {
        add => AddHandler(DragDeltaEvent, value);
        remove => RemoveHandler(DragDeltaEvent, value);
    }

    /// <summary>
    /// Подписка на событие завершения перетаскивания.
    /// </summary>
    public event EventHandler<DragCompletedEventArgs> DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    #endregion

    // Стек состояний (State Machine)
    private readonly Stack<DesignEditorItemState> _states = new();

    /// <summary>
    /// Текущее активное состояние элемента (Idle, Dragging и т.д.).
    /// </summary>
    public DesignEditorItemState CurrentState => _states.Count > 0 ? _states.Peek() : null!;

    static DesignEditorItem()
    {
        // Подключаем стандартную логику выбора Avalonia
        SelectableMixin.Attach<DesignEditorItem>(IsSelectedProperty);

        FocusableProperty.OverrideDefaultValue<DesignEditorItem>(true);

        // При изменении Location заставляем родительскую панель пересчитать Layout
        LocationProperty.Changed.AddClassHandler<DesignEditorItem>((item, args) =>
        {
            if (item.GetVisualParent() is Panel panel) panel.InvalidateArrange();
        });
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DesignEditorItem"/>.
    /// </summary>
    public DesignEditorItem()
    {
        // Инициализируем начальное состояние (Idle)
        _states.Push(new ItemIdleState(this));
    }

    #region Property Changed Logic

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // Пересчитываем SelectedMargin, если изменилась толщина границ (обычная или при выделении)
        if (change.Property == BorderThicknessProperty ||
            change.Property == SelectedBorderThicknessProperty)
        {
            // Уведомляем систему свойств об изменении вычисляемого свойства
            RaisePropertyChanged(SelectedMarginProperty, default, SelectedMargin);
        }
    }

    #endregion

    #region State Machine Management

    /// <summary>
    /// Переключает элемент в новое состояние, сохраняя предыдущее в стеке.
    /// </summary>
    /// <param name="state">Новое состояние.</param>
    public void PushState(DesignEditorItemState state)
    {
        var previous = CurrentState;
        _states.Push(state);
        state.Enter(previous);
    }

    /// <summary>
    /// Возвращает элемент к предыдущему состоянию.
    /// </summary>
    public void PopState()
    {
        if (_states.Count > 1) // Никогда не удаляем корневое состояние (Idle)
        {
            var current = _states.Pop();
            current.Exit();
            CurrentState.ReEnter(current);
        }
    }

    #endregion

    #region Event Handlers (Delegate to State)

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled)
        {
            CurrentState.OnPointerPressed(e);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        CurrentState.OnPointerMoved(e);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        CurrentState.OnPointerReleased(e);
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // При потере захвата (например, Alt-Tab или системное прерывание)
        // сбрасываем состояние до исходного (Idle)
        while (_states.Count > 1)
        {
            PopState();
        }
    }

    #endregion
}
