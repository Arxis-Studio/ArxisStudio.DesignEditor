using Avalonia;

namespace ArxisStudio;

public interface IDesignEditorItem
{
    /// <summary>
    /// Позиция элемента на холсте.
    /// Используется панелью DesignPanel для ускорения Layout (вместо медленных Canvas.Left/Top).
    /// </summary>
    Point Location { get; }
}
