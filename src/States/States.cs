using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace ArxisStudio.States;

/// <summary>
/// Базовый класс состояния
/// </summary>
public abstract class DesignEditorItemState
{
    protected DesignEditorItem Container { get; }

    protected DesignEditorItemState(DesignEditorItem container) => Container = container;

    public virtual void Enter(DesignEditorItemState from) { }
    public virtual void Exit() { }
    public virtual void ReEnter(DesignEditorItemState from) { }

    public virtual void OnPointerPressed(PointerPressedEventArgs e) { }
    public virtual void OnPointerMoved(PointerEventArgs e) { }
    public virtual void OnPointerReleased(PointerReleasedEventArgs e) { }
}

/// <summary>
/// Состояние покоя. Обрабатывает выделение и начало драга.
/// </summary>
public class ItemIdleState : DesignEditorItemState
{
    private Point _startPoint;
    private bool _isPressed;
    private bool _shouldSkipSelectionToggle;

    public ItemIdleState(DesignEditorItem container) : base(container) { }

    public override void ReEnter(DesignEditorItemState from)
    {
        _isPressed = false;
        _shouldSkipSelectionToggle = false;
    }

    public override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(Container).Properties;
        if (props.IsLeftButtonPressed && Container.IsDraggable)
        {
            e.Pointer.Capture(Container);
            e.Handled = true; // Перехватываем событие у редактора

            _isPressed = true;
            // Получаем координаты относительно родителя для корректного драга
            var parent = Container.GetVisualParent();
            if (parent != null)
                _startPoint = e.GetPosition((Visual)parent);

            HandleSelectionOnPress(e);
        }
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isPressed) return;

        var parent = Container.GetVisualParent();
        if (parent == null) return;

        var currentPoint = e.GetPosition((Visual)parent);
        var distance = Vector.Distance(_startPoint, currentPoint);

        // Порог 3 пикселя
        if (distance > 3)
        {
            // Переход в DraggingState
            Container.PushState(new ItemDraggingState(Container, _startPoint));
        }
    }

    public override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_isPressed)
        {
            // Если драг не начался, завершаем логику клика (выделение)
            HandleSelectionOnRelease(e);

            _isPressed = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void HandleSelectionOnPress(PointerPressedEventArgs e)
    {
        var editor = Container.FindAncestorOfType<DesignEditor>();
        if (editor == null) return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (!Container.IsSelected)
        {
            if (!isCtrl) editor.Selection.Clear();
            editor.Selection.Select(editor.IndexFromContainer(Container));
            _shouldSkipSelectionToggle = true;
        }
        else
        {
            _shouldSkipSelectionToggle = false;
        }
    }

    private void HandleSelectionOnRelease(PointerReleasedEventArgs e)
    {
        var editor = Container.FindAncestorOfType<DesignEditor>();
        if (editor == null) return;

        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var index = editor.IndexFromContainer(Container);

        if (isCtrl)
        {
            if (!_shouldSkipSelectionToggle)
            {
                if (Container.IsSelected) editor.Selection.Deselect(index);
                else editor.Selection.Select(index);
            }
        }
        else
        {
            // Клик без Ctrl по уже выбранному элементу (сброс группы)
            if (Container.IsSelected && editor.Selection.Count > 1)
            {
                editor.Selection.Clear();
                editor.Selection.Select(index);
            }
        }
    }
}

/// <summary>
/// Состояние активного перетаскивания.
/// </summary>
public class ItemDraggingState : DesignEditorItemState
{
    private Point _previousPosition;
    private readonly Point _initialPosition;

    public ItemDraggingState(DesignEditorItem container, Point initialPosition) : base(container)
    {
        _initialPosition = initialPosition;
        _previousPosition = initialPosition;
    }

    public override void Enter(DesignEditorItemState from)
    {
        // Nodify style: DragStarted вызывается при входе в состояние
        var args = new DragStartedEventArgs(_initialPosition.X, _initialPosition.Y)
        {
            RoutedEvent = DesignEditorItem.DragStartedEvent
        };
        Container.RaiseEvent(args);
    }

    public override void Exit()
    {
        // Nodify style: DragCompleted вызывается при выходе из состояния
        var totalDelta = _previousPosition - _initialPosition;
        var args = new DragCompletedEventArgs(totalDelta.X, totalDelta.Y, false)
        {
            RoutedEvent = DesignEditorItem.DragCompletedEvent
        };
        Container.RaiseEvent(args);
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        var parent = Container.GetVisualParent();
        if (parent == null) return;

        var currentPosition = e.GetPosition((Visual)parent);
        var delta = currentPosition - _previousPosition;

        if (Math.Abs(delta.X) > double.Epsilon || Math.Abs(delta.Y) > double.Epsilon)
        {
            var args = new DragDeltaEventArgs(delta.X, delta.Y)
            {
                RoutedEvent = DesignEditorItem.DragDeltaEvent
            };
            Container.RaiseEvent(args);

            _previousPosition = currentPosition;
        }
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        // Завершение перетаскивания -> Возврат в Idle
        Container.PopState(); // Вызовет Exit() -> DragCompleted

        e.Pointer.Capture(null);
        e.Handled = true;
    }
}
