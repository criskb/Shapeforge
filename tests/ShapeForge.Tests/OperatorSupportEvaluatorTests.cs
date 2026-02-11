using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public sealed class OperatorSupportEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsSupported_WhenModeQualityAndBackendsMatch()
    {
        var schema = new RepairFixOperator().Schema;

        var result = OperatorSupportEvaluator.Evaluate(
            schema,
            ProcessMode.Fdm,
            PresetQuality.Final,
            BackendCapabilityFlags.FastMesh);

        Assert.Equal(OperatorSupportLevel.Supported, result.Level);
    }

    [Fact]
    public void Evaluate_ReturnsNotSupported_WhenModeIsExcluded()
    {
        var schema = new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate).Schema;

        var result = OperatorSupportEvaluator.Evaluate(
            schema,
            ProcessMode.Resin,
            PresetQuality.Final,
            BackendCapabilityFlags.FastMesh | BackendCapabilityFlags.Voxel);

        Assert.Equal(OperatorSupportLevel.NotSupported, result.Level);
        Assert.Contains("unsupported", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReturnsLimited_WhenRequiredBackendIsMissing()
    {
        var schema = new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate).Schema;

        var result = OperatorSupportEvaluator.Evaluate(
            schema,
            ProcessMode.Fdm,
            PresetQuality.Preview,
            BackendCapabilityFlags.FastMesh);

        Assert.Equal(OperatorSupportLevel.Limited, result.Level);
        Assert.Contains("missing backend capability", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
