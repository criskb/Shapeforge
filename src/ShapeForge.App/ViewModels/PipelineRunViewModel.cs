using System.Collections.ObjectModel;

namespace ShapeForge.App.ViewModels;

public sealed class PipelineRunViewModel : ObservableObject
{
    private string _elapsed = "-";
    private string _comparisonSummary = "Run not completed yet.";

    public string Elapsed
    {
        get => _elapsed;
        set => SetProperty(ref _elapsed, value);
    }

    public string ComparisonSummary
    {
        get => _comparisonSummary;
        set => SetProperty(ref _comparisonSummary, value);
    }

    public ObservableCollection<PipelineStepViewModel> Steps { get; } = [];
}

public sealed record PipelineStepViewModel(string Name, string Duration, int WarningCount);
