using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // ВАЖНО: Для RelayCommand

namespace DesignEditor.Demo.ViewModels;

// Базовый класс для любого элемента на холсте
public partial class DesignItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Location))]
    private double _x;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Location))]
    private double _y;

    // Свойство для быстрого биндинга к IDesignEditorItem.Location
    public Point Location
    {
        get => new Point(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    protected DesignItemViewModel(double x, double y)
    {
        X = x;
        Y = y;
    }
}

// Модели
public class LoginNodeViewModel : DesignItemViewModel
{
    public LoginNodeViewModel(double x, double y) : base(x, y) { }
}

public class DashboardNodeViewModel : DesignItemViewModel
{
    public DashboardNodeViewModel(double x, double y) : base(x, y) { }
}

public partial class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<DesignItemViewModel> Nodes { get; } = new();

    // СПИСОК ВЫДЕЛЕННЫХ (Avalonia работает с object, поэтому IList или ObservableCollection<object>)
    [ObservableProperty]
    private ObservableCollection<object> _selectedNodes = new();

    // --- ZOOM ---
    [ObservableProperty]
    private double _zoom = 1.0;

    // --- RESET COMMAND ---
    // CommunityToolkit сгенерирует свойство "ResetZoomCommand"
    [RelayCommand]
    public void ResetZoom()
    {
        Zoom = 1.0;
    }

    public MainWindowViewModel()
    {
        Nodes.Add(new LoginNodeViewModel(400, 300));
        Nodes.Add(new DashboardNodeViewModel(800, 300));
        Nodes.Add(new LoginNodeViewModel(100, 100));
        Nodes.Add(new DashboardNodeViewModel(100, 450));
    }
}
