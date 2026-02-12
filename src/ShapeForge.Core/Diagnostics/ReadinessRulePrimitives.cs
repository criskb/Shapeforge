namespace ShapeForge.Core.Diagnostics;

public interface IReadinessRule
{
    string Name { get; }
    ReadinessIssue? Evaluate(MeshDiagnostics diagnostics, IReadinessProfile profile);
}

public interface IReadinessProfile
{
    float MinWallMm { get; }
    float OverhangThresholdDeg { get; }
    float MinimumDrainHoleMm { get; }
    string Mode { get; }
    string Quality { get; }
}

public sealed class ThresholdRule : IReadinessRule
{
    private readonly string _metricKey;
    private readonly Func<IReadinessProfile, double> _threshold;
    private readonly Func<double, double, bool> _isFailure;
    private readonly string _issueCode;
    private readonly IssueSeverity _severity;
    private readonly string _message;
    private readonly int _priority;
    private readonly string _hint;

    public ThresholdRule(
        string name,
        string metricKey,
        Func<IReadinessProfile, double> threshold,
        Func<double, double, bool> isFailure,
        string issueCode,
        IssueSeverity severity,
        string message,
        int priority,
        string hint)
    {
        Name = name;
        _metricKey = metricKey;
        _threshold = threshold;
        _isFailure = isFailure;
        _issueCode = issueCode;
        _severity = severity;
        _message = message;
        _priority = priority;
        _hint = hint;
    }

    public string Name { get; }

    public ReadinessIssue? Evaluate(MeshDiagnostics diagnostics, IReadinessProfile profile)
    {
        if (!TryGetMetric(diagnostics, _metricKey, out var metricValue))
        {
            return null;
        }

        var thresholdValue = _threshold(profile);
        if (!_isFailure(metricValue, thresholdValue))
        {
            return null;
        }

        return new ReadinessIssue(
            _issueCode,
            _severity,
            _message,
            _hint,
            _priority,
            Name,
            Confidence: 0.85);
    }

    private static bool TryGetMetric(MeshDiagnostics diagnostics, string key, out double value)
    {
        if (diagnostics.Printability.TryGetValue(key, out value) ||
            diagnostics.Topology.TryGetValue(key, out value) ||
            diagnostics.Quality.TryGetValue(key, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}

public sealed class RatioRule : IReadinessRule
{
    private readonly string _numeratorMetric;
    private readonly string _denominatorMetric;
    private readonly Func<IReadinessProfile, double> _maxRatio;
    private readonly string _issueCode;
    private readonly IssueSeverity _severity;
    private readonly string _message;
    private readonly int _priority;
    private readonly string _hint;

    public RatioRule(
        string name,
        string numeratorMetric,
        string denominatorMetric,
        Func<IReadinessProfile, double> maxRatio,
        string issueCode,
        IssueSeverity severity,
        string message,
        int priority,
        string hint)
    {
        Name = name;
        _numeratorMetric = numeratorMetric;
        _denominatorMetric = denominatorMetric;
        _maxRatio = maxRatio;
        _issueCode = issueCode;
        _severity = severity;
        _message = message;
        _priority = priority;
        _hint = hint;
    }

    public string Name { get; }

    public ReadinessIssue? Evaluate(MeshDiagnostics diagnostics, IReadinessProfile profile)
    {
        if (!TryGetMetric(diagnostics, _numeratorMetric, out var numerator) ||
            !TryGetMetric(diagnostics, _denominatorMetric, out var denominator) ||
            denominator <= 0)
        {
            return null;
        }

        var ratio = numerator / denominator;
        var maxRatio = _maxRatio(profile);
        if (ratio <= maxRatio)
        {
            return null;
        }

        return new ReadinessIssue(
            _issueCode,
            _severity,
            _message,
            _hint,
            _priority,
            Name,
            Confidence: 0.8);
    }

    private static bool TryGetMetric(MeshDiagnostics diagnostics, string key, out double value)
    {
        if (diagnostics.Printability.TryGetValue(key, out value) ||
            diagnostics.Topology.TryGetValue(key, out value) ||
            diagnostics.Quality.TryGetValue(key, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}

public sealed class TopologyRule : IReadinessRule
{
    private readonly string _boolMetricKey;
    private readonly bool _expectedValue;
    private readonly string _issueCode;
    private readonly IssueSeverity _severity;
    private readonly string _message;
    private readonly int _priority;
    private readonly string _hint;

    public TopologyRule(
        string name,
        string boolMetricKey,
        bool expectedValue,
        string issueCode,
        IssueSeverity severity,
        string message,
        int priority,
        string hint)
    {
        Name = name;
        _boolMetricKey = boolMetricKey;
        _expectedValue = expectedValue;
        _issueCode = issueCode;
        _severity = severity;
        _message = message;
        _priority = priority;
        _hint = hint;
    }

    public string Name { get; }

    public ReadinessIssue? Evaluate(MeshDiagnostics diagnostics, IReadinessProfile profile)
    {
        _ = profile;
        if (!diagnostics.BooleanFlags.TryGetValue(_boolMetricKey, out var currentValue))
        {
            return null;
        }

        if (currentValue == _expectedValue)
        {
            return null;
        }

        return new ReadinessIssue(
            _issueCode,
            _severity,
            _message,
            _hint,
            _priority,
            Name,
            Confidence: 0.9);
    }
}
