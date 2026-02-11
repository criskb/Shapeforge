using System.Collections.ObjectModel;

namespace ShapeForge.App.ViewModels;

public sealed class OperatorStackViewModel : ObservableObject
{
    public ObservableCollection<OperatorItemViewModel> Operators { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public string Summary => Operators.Count == 0
        ? "No operators built yet."
        : Warnings.Count == 0
            ? $"{Operators.Count} operators ready."
            : $"{Operators.Count} operators ready ({Warnings.Count} compatibility warning(s)).";

    public void RaiseSummaryChanged() => OnPropertyChanged(nameof(Summary));
}

public sealed record OperatorItemViewModel(string Id, string DisplayName, string Category, string Version);
