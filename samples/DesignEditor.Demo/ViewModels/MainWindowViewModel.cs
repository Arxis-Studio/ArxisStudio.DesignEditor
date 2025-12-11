using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel; // Если используется CommunityToolkit
// Или просто реализуйте INotifyPropertyChanged вручную

namespace DesignEditor.Demo.ViewModels;

// Базовый класс для любого элемента на холсте
public abstract class DesignItemViewModel
{
    public double X { get; set; }
    public double Y { get; set; }

    protected DesignItemViewModel(double x, double y)
    {
        X = x;
        Y = y;
    }
}

// Модель для формы входа
public class LoginNodeViewModel : DesignItemViewModel
{
    public LoginNodeViewModel(double x, double y) : base(x, y) { }
}

// Модель для дашборда
public class DashboardNodeViewModel : DesignItemViewModel
{
    public DashboardNodeViewModel(double x, double y) : base(x, y) { }
}

public partial class MainWindowViewModel : ObservableObject
{
    // Все элементы на холсте
    public ObservableCollection<DesignItemViewModel> Nodes { get; } = new();

    // СПИСОК ВЫДЕЛЕННЫХ ЭЛЕМЕНТОВ (Сюда DesignEditor будет писать данные)
    [ObservableProperty]
    private ObservableCollection<DesignItemViewModel> _selectedNodes = new();

    public MainWindowViewModel()
    {
        // Добавляем тестовые данные (то, что раньше было в XAML)
        Nodes.Add(new LoginNodeViewModel(400, 300));
        Nodes.Add(new DashboardNodeViewModel(800, 300));

        // Добавим еще пару для теста
        Nodes.Add(new LoginNodeViewModel(100, 100));
    }
}
