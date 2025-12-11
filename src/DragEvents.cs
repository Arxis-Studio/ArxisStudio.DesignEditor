using Avalonia.Interactivity;

namespace ArxisStudio;

public class DragStartedEventArgs : RoutedEventArgs
{
    public double HorizontalOffset { get; }
    public double VerticalOffset { get; }

    public DragStartedEventArgs(double horizontalOffset, double verticalOffset)
    {
        HorizontalOffset = horizontalOffset;
        VerticalOffset = verticalOffset;
    }
}

public class DragDeltaEventArgs : RoutedEventArgs
{
    public double HorizontalChange { get; }
    public double VerticalChange { get; }

    public DragDeltaEventArgs(double horizontalChange, double verticalChange)
    {
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
    }
}

public class DragCompletedEventArgs : RoutedEventArgs
{
    public double HorizontalChange { get; }
    public double VerticalChange { get; }
    public bool Canceled { get; }

    public DragCompletedEventArgs(double horizontalChange, double verticalChange, bool canceled)
    {
        HorizontalChange = horizontalChange;
        VerticalChange = verticalChange;
        Canceled = canceled;
    }
}
