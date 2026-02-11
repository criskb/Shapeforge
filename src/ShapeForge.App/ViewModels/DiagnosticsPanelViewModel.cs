using System.Collections.ObjectModel;

namespace ShapeForge.App.ViewModels;

public sealed class DiagnosticsPanelViewModel : ObservableObject
{
    private string _triangleCount = "-";
    private string _volumeDelta = "-";
    private string _minThickness = "-";
    private string _trappedVolumes = "-";

    public string TriangleCount
    {
        get => _triangleCount;
        set => SetProperty(ref _triangleCount, value);
    }

    public string VolumeDelta
    {
        get => _volumeDelta;
        set => SetProperty(ref _volumeDelta, value);
    }

    public string MinThickness
    {
        get => _minThickness;
        set => SetProperty(ref _minThickness, value);
    }

    public string TrappedVolumes
    {
        get => _trappedVolumes;
        set => SetProperty(ref _trappedVolumes, value);
    }

    public ObservableCollection<DiagnosticIssueItemViewModel> Issues { get; } = [];
}

public sealed record DiagnosticIssueItemViewModel(string Severity, string Code, string Message, int Count)
{
    public string Display => $"[{Severity}] {Code} — {Message}";
}

