using ShapeForge.App.Mapping;
using ShapeForge.App.State;
using ShapeForge.Core.Diagnostics;
using ShapeForge.Core.Geometry;
using ShapeForge.Core.IO;
using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace ShapeForge.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private enum WorkflowStage
    {
        None,
        Loaded,
        Diagnosed,
        StackBuilt,
        RunCompleted,
        Compared,
        Exported
    }

    private readonly ReadinessEvaluator _readinessEvaluator = new();
    private readonly PipelineRunner _pipelineRunner = new();
    private readonly AppStateStore _stateStore = new();
    private readonly AppUiState _state;
    private readonly ObservableCollection<string> _flowSteps =
    [
        "Load",
        "Diagnose",
        "Build stack",
        "Run",
        "Compare",
        "Export"
    ];

    private WorkflowStage _stage = WorkflowStage.None;
    private string _selectedPreset = "Fdm";
    private string _selectedRecipe = "Default Repair";
    private string _selectedUnits = "mm";
    private string _statusMessage = "Ready";

    private MeshModel? _loadedMesh;
    private MeshDiagnostics? _preDiagnostics;
    private MeshDiagnostics? _postDiagnostics;
    private ReadinessResult? _readiness;
    private PipelineRunResult? _runResult;
    private List<IOperator> _operatorStack = [];

    public MainWindowViewModel()
    {
        _state = _stateStore.Load();
        _selectedPreset = _state.LastProfile;
        _selectedRecipe = _state.LastRecipe;
        _selectedUnits = _state.LastUnits;

        LoadCommand = new RelayCommand(LoadMesh);
        DiagnoseCommand = new RelayCommand(Diagnose, () => _stage >= WorkflowStage.Loaded);
        BuildStackCommand = new RelayCommand(BuildStack, () => _stage >= WorkflowStage.Diagnosed);
        RunCommand = new RelayCommand(Run, () => _stage >= WorkflowStage.StackBuilt);
        CompareCommand = new RelayCommand(Compare, () => _stage >= WorkflowStage.RunCompleted);
        ExportCommand = new RelayCommand(Export, () => _stage >= WorkflowStage.Compared);

        DiagnosticsPanel = new DiagnosticsPanelViewModel();
        OperatorStack = new OperatorStackViewModel();
        PipelineRun = new PipelineRunViewModel();
        ReadinessSummary = new ReadinessSummaryViewModel();
    }

    public IReadOnlyList<string> PresetOptions { get; } = ["Fdm", "Sla", "Sls"];
    public IReadOnlyList<string> RecipeOptions { get; } = ["Default Repair", "Strength First", "Speed First"];
    public IReadOnlyList<string> UnitOptions { get; } = ["mm", "in"];
    public IReadOnlyList<string> FlowSteps => _flowSteps;

    public string SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                PersistUiState();
            }
        }
    }

    public string SelectedRecipe
    {
        get => _selectedRecipe;
        set
        {
            if (SetProperty(ref _selectedRecipe, value))
            {
                PersistUiState();
            }
        }
    }

    public string SelectedUnits
    {
        get => _selectedUnits;
        set
        {
            if (SetProperty(ref _selectedUnits, value))
            {
                PersistUiState();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public DiagnosticsPanelViewModel DiagnosticsPanel { get; }
    public OperatorStackViewModel OperatorStack { get; }
    public PipelineRunViewModel PipelineRun { get; }
    public ReadinessSummaryViewModel ReadinessSummary { get; }

    public ICommand LoadCommand { get; }
    public ICommand DiagnoseCommand { get; }
    public ICommand BuildStackCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand CompareCommand { get; }
    public ICommand ExportCommand { get; }

    private void LoadMesh()
    {
        _loadedMesh = BuildSampleMesh();
        _preDiagnostics = null;
        _postDiagnostics = null;
        _readiness = null;
        _runResult = null;

        DiagnosticsPanel.Issues.Clear();
        OperatorStack.Operators.Clear();
        OperatorStack.RaiseSummaryChanged();
        PipelineRun.Steps.Clear();
        PipelineRun.Elapsed = "-";
        PipelineRun.ComparisonSummary = "Run not completed yet.";
        ReadinessSummary.TopBlockers.Clear();
        ReadinessSummary.Status = "-";
        ReadinessSummary.Grade = "-";
        ReadinessSummary.Confidence = "-";

        MoveToStage(WorkflowStage.Loaded, "Mesh loaded. Next: Diagnose.");
    }

    private void Diagnose()
    {
        if (_loadedMesh is null)
        {
            return;
        }

        _preDiagnostics = ReportCard.Build(_loadedMesh);
        CoreToUiMapper.MapDiagnostics(DiagnosticsPanel, _preDiagnostics);

        var profile = ResolveProfile();
        _readiness = _readinessEvaluator.Evaluate(_preDiagnostics, profile);
        CoreToUiMapper.MapReadiness(ReadinessSummary, _readiness);

        MoveToStage(WorkflowStage.Diagnosed, "Diagnostics complete. Next: Build operator stack.");
    }

    private void BuildStack()
    {
        _operatorStack = ResolveOperators(ResolveProfile());
        CoreToUiMapper.MapOperatorStack(OperatorStack, _operatorStack);
        MoveToStage(WorkflowStage.StackBuilt, "Operator stack built. Next: Run pipeline.");
    }

    private void Run()
    {
        if (_loadedMesh is null || _operatorStack.Count == 0)
        {
            return;
        }

        var profile = ResolveProfile();
        var context = new OperatorContext(
            profile.VoxelSizeMm,
            new Progress<float>(_ => { }),
            _ => { },
            new Dictionary<string, object>(),
            profile.Units,
            profile.Mode,
            profile.Quality,
            profile.MinWallPolicy,
            profile.MinWallMm,
            profile.OverhangThresholdDeg,
            profile.MinimumDrainHoleMm,
            profile.RepairMode,
            ExecutionMode.HighQuality,
            QualityScalingPolicy.For(ExecutionMode.HighQuality),
            Seed: 1337);

        _runResult = _pipelineRunner.RunDetailedAsync(_loadedMesh, _operatorStack, context, CancellationToken.None).GetAwaiter().GetResult();
        _postDiagnostics = _runResult.PostDiagnostics;
        CoreToUiMapper.MapPipelineRun(PipelineRun, _runResult);

        MoveToStage(WorkflowStage.RunCompleted, "Pipeline run completed. Next: Compare pre/post diagnostics.");
    }

    private void Compare()
    {
        if (_preDiagnostics is null || _postDiagnostics is null)
        {
            return;
        }

        var preIssues = _preDiagnostics.Issues.Count(i => i.Severity >= IssueSeverity.Warning);
        var postIssues = _postDiagnostics.Issues.Count(i => i.Severity >= IssueSeverity.Warning);
        var delta = postIssues - preIssues;
        var direction = delta <= 0 ? "improved" : "regressed";

        PipelineRun.ComparisonSummary =
            $"Warnings/errors: {preIssues} → {postIssues} ({Math.Abs(delta)} {direction}).";

        CoreToUiMapper.MapDiagnostics(DiagnosticsPanel, _postDiagnostics);
        var readiness = _readinessEvaluator.Evaluate(_postDiagnostics, ResolveProfile());
        CoreToUiMapper.MapReadiness(ReadinessSummary, readiness);

        MoveToStage(WorkflowStage.Compared, "Comparison done. Next: Export mesh.");
    }

    private void Export()
    {
        if (_runResult is null)
        {
            return;
        }

        var exportFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ShapeForge", "Exports");
        Directory.CreateDirectory(exportFolder);
        var exportPath = Path.Combine(exportFolder, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{SelectedRecipe.Replace(' ', '-')}.stl");

        var io = new StlMeshIO();
        io.SaveStlAsync(exportPath, _runResult.FinalMesh).GetAwaiter().GetResult();

        MoveToStage(WorkflowStage.Exported, $"Exported: {exportPath}");
    }

    private void MoveToStage(WorkflowStage stage, string message)
    {
        _stage = stage;
        StatusMessage = message;
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        ((RelayCommand)DiagnoseCommand).RaiseCanExecuteChanged();
        ((RelayCommand)BuildStackCommand).RaiseCanExecuteChanged();
        ((RelayCommand)RunCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CompareCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportCommand).RaiseCanExecuteChanged();
    }

    private void PersistUiState()
    {
        _state.LastProfile = SelectedPreset;
        _state.LastRecipe = SelectedRecipe;
        _state.LastUnits = SelectedUnits;
        _stateStore.Save(_state);
    }

    private PresetParameters ResolveProfile()
    {
        var parsedPreset = Enum.TryParse<PrintPreset>(SelectedPreset, true, out var preset)
            ? preset
            : PrintPreset.Fdm;

        return Presets.Resolve(
            parsedPreset,
            unitsOverride: SelectedUnits,
            modeOverride: null,
            qualityOverride: SelectedRecipe == "Speed First" ? PresetQuality.Preview : PresetQuality.Final,
            repairModeOverride: SelectedRecipe == "Strength First" ? RepairMode.Aggressive : null);
    }

    private static List<IOperator> ResolveOperators(PresetParameters profile)
    {
        var operators = new List<IOperator>
        {
            new RepairFixOperator(),
            new ThicknessEnforceOperator(profile.MinWallMm, profile.ThicknessMode)
        };

        return operators;
    }

    private static MeshModel BuildSampleMesh()
    {
        var vertices = new float[]
        {
            0,0,0,
            1,0,0,
            1,1,0,
            0,1,0,
            0,0,1,
            1,0,1,
            1,1,1,
            0,1,1
        };

        var indices = new int[]
        {
            0,1,2, 0,2,3,
            4,5,6, 4,6,7,
            0,1,5, 0,5,4,
            2,3,7, 2,7,6,
            1,2,6, 1,6,5,
            0,3,7, 0,7,4
        };

        return new MeshModel(vertices, indices, Normals: null, Units: "mm");
    }
}
