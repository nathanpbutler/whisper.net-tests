using System.Collections.ObjectModel;
using avalonia.mvvm.Models;

namespace avalonia.mvvm.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    // public string SelectedFilePath { get; set; } = string.Empty;
    // public string Status { get; set; } = string.Empty;

    public ObservableCollection<Segment> Segments { get; } = [];
}
