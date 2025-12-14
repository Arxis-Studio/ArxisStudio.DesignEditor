using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using ArxisStudio.Controls; // Пространство имен с ResizeDirection и ResizeDeltaEventArgs

namespace ArxisStudio.States;

/// <summary>
/// Базовый абстрактный класс состояния для элемента дизайнера (<see cref="DesignEditorItem"/>).
/// Реализует паттерн State.
/// </summary>
public abstract class DesignEditorItemState
{
    /// <summary>
    /// Контекст (элемент), которым управляет данное состояние.
    /// </summary>
    protected DesignEditorItem Container { get; }

    /// <summary>
    /// Инициализирует новый экземпляр состояния.
    /// </summary>
    /// <param name="container">Элемент дизайнера, к которому привязано состояние.</param>
    protected DesignEditorItemState(DesignEditorItem container) => Container = container;

    /// <summary>
    /// Вызывается при входе в данное состояние.
    /// </summary>
    /// <param name="from">Предыдущее состояние.</param>
    public virtual void Enter(DesignEditorItemState from) { }

    /// <summary>
    /// Вызывается при выходе из данного состояния.
    /// </summary>
    public virtual void Exit() { }

    /// <summary>
    /// Вызывается при повторном входе в состояние (например, после возврата из дочернего состояния).
    /// </summary>
    /// <param name="from">Состояние, из которого происходит возврат.</param>
    public virtual void ReEnter(DesignEditorItemState from) { }

    /// <summary>
    /// Обрабатывает событие нажатия кнопки мыши.
    /// </summary>
    /// <param name="e">Аргументы события нажатия.</param>
    public virtual void OnPointerPressed(PointerPressedEventArgs e) { }

    /// <summary>
    /// Обрабатывает событие перемещения мыши.
    /// </summary>
    /// <param name="e">Аргументы события перемещения.</param>
    public virtual void OnPointerMoved(PointerEventArgs e) { }

    /// <summary>
    /// Обрабатывает событие отпускания кнопки мыши.
    /// </summary>
    /// <param name="e">Аргументы события отпускания.</param>
    public virtual void OnPointerReleased(PointerReleasedEventArgs e) { }

    /// <summary>
    /// Обрабатывает событие изменения размера, инициированное <see cref="ResizeAdorner"/>.
    /// </summary>
    /// <param name="e">Аргументы события изменения размера.</param>
    public virtual void OnResizeDelta(ResizeDeltaEventArgs e) { }
}

/// <summary>
/// Состояние покоя (Idle).
/// Ожидает действий пользователя: выделения (Selection) или начала перетаскивания (Drag).
/// </summary>
public class ItemIdleState : DesignEditorItemState
{
    private Point _startPoint;
    private bool _isPressed;
    private bool _shouldSkipSelectionToggle;

    /// <summary>
    /// Инициализирует новый экземпляр состояния покоя.
    /// </summary>
    /// <param name="container">Управляемый элемент.</param>
    public ItemIdleState(DesignEditorItem container) : base(container) { }

    /// <inheritdoc />
    public override void ReEnter(DesignEditorItemState from)
    {
        // Сбрасываем флаги при возврате в состояние покоя
        _isPressed = false;
        _shouldSkipSelectionToggle = false;
    }

    /// <inheritdoc />
    public override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(Container).Properties;

        // Реагируем только на левую кнопку мыши, если элемент перетаскиваемый
        if (props.IsLeftButtonPressed && Container.IsDraggable)
        {
            e.Pointer.Capture(Container);
            e.Handled = true; // Перехватываем событие, чтобы родитель не обрабатывал его как клик по пустому месту

            _isPressed = true;

            // Запоминаем точку нажатия относительно родителя для расчета дистанции драга
            var parent = Container.GetVisualParent();
            if (parent != null)
                _startPoint = e.GetPosition((Visual)parent);

            // Обрабатываем логику выделения при нажатии
            HandleSelectionOnPress(e);
        }
    }

    /// <inheritdoc />
    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isPressed) return;

        var parent = Container.GetVisualParent();
        if (parent == null) return;

        var currentPoint = e.GetPosition((Visual)parent);
        var distance = Vector.Distance(_startPoint, currentPoint);

        // Порог начала драга (3 пикселя), чтобы избежать случайных смещений при клике
        if (distance > 3)
        {
            // Переходим в состояние перетаскивания
            Container.PushState(new ItemDraggingState(Container, _startPoint));
        }
    }

    /// <inheritdoc />
    public override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isPressed)
        {
            // Если драг не начался и мышь отпустили - завершаем логику выделения
            HandleSelectionOnRelease(e);

            _isPressed = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Логика выделения при нажатии кнопки (MouseDown).
    /// </summary>
    private void HandleSelectionOnPress(PointerPressedEventArgs e)
    {
        var editor = Container.FindAncestorOfType<DesignEditor>();
        if (editor == null) return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // Если элемент еще не выбран
        if (!Container.IsSelected)
        {
            // Если Ctrl не нажат, сбрасываем текущее выделение
            if (!isCtrl) editor.Selection.Clear();

            // Выделяем текущий элемент
            editor.Selection.Select(editor.IndexFromContainer(Container));

            // Ставим флаг, чтобы при отпускании мыши не сбросить выделение
            _shouldSkipSelectionToggle = true;
        }
        else
        {
            _shouldSkipSelectionToggle = false;
        }
    }

    /// <summary>
    /// Логика выделения при отпускании кнопки (MouseUp).
    /// </summary>
    private void HandleSelectionOnRelease(PointerReleasedEventArgs e)
    {
        var editor = Container.FindAncestorOfType<DesignEditor>();
        if (editor == null) return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var index = editor.IndexFromContainer(Container);

        if (isCtrl)
        {
            // Инвертируем выделение при Ctrl+Click, если это не было сделано при нажатии
            if (!_shouldSkipSelectionToggle)
            {
                if (Container.IsSelected) editor.Selection.Deselect(index);
                else editor.Selection.Select(index);
            }
        }
        else
        {
            // Клик без Ctrl по уже выбранному элементу сбрасывает группу выделения (оставляет только этот)
            if (Container.IsSelected && editor.Selection.Count > 1)
            {
                editor.Selection.Clear();
                editor.Selection.Select(index);
            }
        }
    }
}

/// <summary>
/// Состояние активного перетаскивания (Dragging).
/// Управляет перемещением элемента и обновлением его координат.
/// </summary>
public class ItemDraggingState : DesignEditorItemState
{
    private Point _previousPosition;
    private readonly Point _initialPosition; // Точка нажатия мыши (координаты родителя)
    private Point _elementStartLocation;     // Исходная позиция элемента (Location)

    /// <summary>
    /// Инициализирует новый экземпляр состояния перетаскивания.
    /// </summary>
    /// <param name="container">Перетаскиваемый элемент.</param>
    /// <param name="initialPosition">Начальная позиция курсора мыши.</param>
    public ItemDraggingState(DesignEditorItem container, Point initialPosition) : base(container)
    {
        _initialPosition = initialPosition;
        _previousPosition = initialPosition;
    }

    /// <inheritdoc />
    public override void Enter(DesignEditorItemState from)
    {
        // Запоминаем исходные координаты элемента перед началом движения
        _elementStartLocation = Container.Location;

        // Генерируем событие начала драга
        var args = new DragStartedEventArgs(_initialPosition.X, _initialPosition.Y)
        {
            RoutedEvent = DesignEditorItem.DragStartedEvent
        };
        Container.RaiseEvent(args);
    }

    /// <inheritdoc />
    public override void Exit()
    {
        // Генерируем событие завершения драга
        var totalDelta = _previousPosition - _initialPosition;
        var args = new DragCompletedEventArgs(totalDelta.X, totalDelta.Y, false)
        {
            RoutedEvent = DesignEditorItem.DragCompletedEvent
        };
        Container.RaiseEvent(args);
    }

    /// <inheritdoc />
    public override void OnPointerMoved(PointerEventArgs e)
    {
        var parent = Container.GetVisualParent();
        if (parent == null) return;

        var currentPosition = e.GetPosition((Visual)parent);

        // 1. Считаем полный вектор смещения от точки старта (Mouse Start -> Mouse Current)
        // Это предотвращает накопление ошибок округления
        var totalDragVector = currentPosition - _initialPosition;

        // 2. Вычисляем дельту конкретного кадра (для события DragDelta и возможных линий прилипания)
        var frameDelta = currentPosition - _previousPosition;

        // Проверяем, было ли реальное движение
        if (Math.Abs(frameDelta.X) > double.Epsilon || Math.Abs(frameDelta.Y) > double.Epsilon)
        {
            // ОБНОВЛЕНИЕ КООРДИНАТ:
            // Вычисляем новую позицию элемента, прибавляя полный вектор смещения к стартовой позиции.
            double newX = _elementStartLocation.X + totalDragVector.X;
            double newY = _elementStartLocation.Y + totalDragVector.Y;

            // ВАЖНО: Округляем до целых пикселей.
            // Это предотвращает "дребезг" (sub-pixel rendering jitter) и делает интерфейс более четким.
            newX = Math.Round(newX);
            newY = Math.Round(newY);

            // Присваиваем новое значение.
            // Это вызывает OnPropertyChanged -> обновляет Binding (TwoWay) -> обновляет ViewModel.
            Container.Location = new Point(newX, newY);

            // 3. Генерируем событие для внешних подписчиков (например, линий выравнивания)
            var args = new DragDeltaEventArgs(frameDelta.X, frameDelta.Y)
            {
                RoutedEvent = DesignEditorItem.DragDeltaEvent
            };
            Container.RaiseEvent(args);

            _previousPosition = currentPosition;
        }
        e.Handled = true;
    }

    /// <inheritdoc />
    public override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        // Завершаем состояние перетаскивания и возвращаемся в Idle
        Container.PopState();

        e.Pointer.Capture(null);
        e.Handled = true;
    }
}

/// <summary>
/// Состояние изменения размера (Resizing).
/// Управляет изменением ширины, высоты и позиции элемента при взаимодействии с <see cref="ResizeAdorner"/>.
/// </summary>
public class ItemResizingState : DesignEditorItemState
{
    private readonly ResizeDirection _direction;
    private Point _initialLocation;
    private Size _initialSize;

    // Накапливаем изменения (Vector), так как DragDelta в ResizeAdorner дает разницу с прошлого кадра.
    // Накопление необходимо для корректной работы с дробными значениями мыши и предотвращения отставания.
    private Vector _accumulatedDelta;

    /// <summary>
    /// Инициализирует новый экземпляр состояния ресайза.
    /// </summary>
    /// <param name="container">Изменяемый элемент.</param>
    /// <param name="direction">Направление изменения (какая ручка используется).</param>
    public ItemResizingState(DesignEditorItem container, ResizeDirection direction) : base(container)
    {
        _direction = direction;
    }

    /// <inheritdoc />
    public override void Enter(DesignEditorItemState from)
    {
        _initialLocation = Container.Location;
        _accumulatedDelta = Vector.Zero; // Сброс накопленного значения

        // Фиксируем исходные размеры. Если размер Auto (NaN), берем текущий фактический размер (Bounds).
        double w = double.IsNaN(Container.Width) ? Container.Bounds.Width : Container.Width;
        double h = double.IsNaN(Container.Height) ? Container.Bounds.Height : Container.Height;

        // Явно устанавливаем размеры, чтобы элемент перестал быть "Auto"
        Container.Width = w;
        Container.Height = h;
        _initialSize = new Size(w, h);
    }

    /// <inheritdoc />
    public override void OnResizeDelta(ResizeDeltaEventArgs e)
    {
        // 1. Накапливаем пришедшую дельту
        _accumulatedDelta += e.Delta;

        double deltaX = _accumulatedDelta.X;
        double deltaY = _accumulatedDelta.Y;

        // Исходные данные
        double newX = _initialLocation.X;
        double newY = _initialLocation.Y;
        double newW = _initialSize.Width;
        double newH = _initialSize.Height;

        // 2. Рассчитываем новые параметры в зависимости от направления (ручки)
        switch (_direction)
        {
            case ResizeDirection.Right:
                newW += deltaX;
                break;
            case ResizeDirection.Bottom:
                newH += deltaY;
                break;
            case ResizeDirection.Left:
                newW -= deltaX;
                newX += deltaX; // При изменении слева меняется и позиция X
                break;
            case ResizeDirection.Top:
                newH -= deltaY;
                newY += deltaY; // При изменении сверху меняется и позиция Y
                break;
            case ResizeDirection.BottomRight:
                newW += deltaX;
                newH += deltaY;
                break;
            case ResizeDirection.BottomLeft:
                newW -= deltaX;
                newX += deltaX;
                newH += deltaY;
                break;
            case ResizeDirection.TopRight:
                newW += deltaX;
                newH -= deltaY;
                newY += deltaY;
                break;
            case ResizeDirection.TopLeft:
                newW -= deltaX;
                newX += deltaX;
                newH -= deltaY;
                newY += deltaY;
                break;
        }

        // 3. Ограничиваем минимальный размер (10px)
        if (newW < 10) newW = 10;
        if (newH < 10) newH = 10;

        // 4. Округляем значения до целых чисел
        // Это убирает визуальное дрожание (jitter) при субпиксельном рендеринге границ
        newW = Math.Round(newW);
        newH = Math.Round(newH);
        newX = Math.Round(newX);
        newY = Math.Round(newY);

        // 5. Применяем изменения к контейнеру
        // Обновление этих свойств автоматически обновит ViewModel через TwoWay Binding
        Container.Width = newW;
        Container.Height = newH;
        Container.Location = new Point(newX, newY);

        // 6. Пробрасываем событие для внешнего мира (если нужно)
        Container.RaiseEvent(new ResizeDeltaEventArgs(e.Delta, _direction, DesignEditorItem.ResizeDeltaEvent));
    }
}
