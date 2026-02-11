using System.Collections.ObjectModel;

namespace ShapeForge.App.ViewModels;

public sealed class OperatorStackViewModel : ObservableObject
{
    public ObservableCollection<OperatorItemViewModel> Operators { get; } = [];

    public string Summary => Operators.Count == 0
        ? "No operators built yet."
        : $"{Operators.Count} operators ready.";

    public void RaiseSummaryChanged() => OnPropertyChanged(nameof(Summary));
}

public sealed record OperatorItemViewModel(string Id, string DisplayName, string Category, string Version);
