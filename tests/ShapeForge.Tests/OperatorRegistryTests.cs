using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public class OperatorRegistryTests
{
    [Fact]
    public void TryGet_ResolvesDeprecatedOperatorIds()
    {
        var registry = new OperatorRegistry();
        registry.Register(new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate));
        registry.RegisterCompatibilityMap(DeprecatedOperatorIds.Map);

        var found = registry.TryGet("thickness.enforce", out var op);

        Assert.True(found);
        Assert.NotNull(op);
        Assert.Equal(ThicknessEnforceOperator.CanonicalId, op!.Id);
    }

    [Fact]
    public void OperatorSchemas_ExposeCategoryDeterminismAndEstimatedCost()
    {
        var repair = new RepairFixOperator();
        var thickness = new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate);

        Assert.StartsWith("repair.", repair.Schema.Category);
        Assert.StartsWith("prep.fdm.", thickness.Schema.Category);
        Assert.True(repair.Schema.Deterministic);
        Assert.True(thickness.Schema.Deterministic);
        Assert.True(repair.Schema.EstimatedCost > 0);
        Assert.True(thickness.Schema.EstimatedCost > 0);
    }
}
