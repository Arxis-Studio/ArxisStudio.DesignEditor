using Avalonia.Input;

namespace ArxisStudio.States;

/// <summary>
/// Базовый класс состояния редактора.
/// </summary>
public abstract class EditorState
{
    protected DesignEditor Editor { get; }

    protected EditorState(DesignEditor editor)
    {
        Editor = editor;
    }

    public virtual void Enter(EditorState? from) { }
    public virtual void Exit() { }

    public virtual void OnPointerPressed(PointerPressedEventArgs e) { }
    public virtual void OnPointerMoved(PointerEventArgs e) { }
    public virtual void OnPointerReleased(PointerReleasedEventArgs e) { }
    public virtual void OnPointerWheelChanged(PointerWheelEventArgs e) { }
}
