using System.Collections.ObjectModel;

namespace ShapeForge.App.ViewModels;

public sealed class ReadinessSummaryViewModel : ObservableObject
{
    private string _status = "-";
    private string _grade = "-";
    private string _confidence = "-";

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string Grade
    {
        get => _grade;
        set => SetProperty(ref _grade, value);
    }

    public string Confidence
    {
        get => _confidence;
        set => SetProperty(ref _confidence, value);
    }

    public ObservableCollection<ReadinessIssueItemViewModel> TopBlockers { get; } = [];
}

public sealed record ReadinessIssueItemViewModel(string Code, string Message, string Severity)
{
    public string Display => $"{Code} — {Message}";
}

