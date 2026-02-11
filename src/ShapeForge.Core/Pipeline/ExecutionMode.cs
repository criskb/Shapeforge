using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Pipeline;

public enum ExecutionMode
{
    Preview,
    Standard,
    Final
}

public readonly record struct QualityScalingPolicy(
    float SamplingDensityScale,
    float VoxelSizeScale,
    int SmoothingPasses)
{
    public static QualityScalingPolicy ForMode(ExecutionMode mode) => mode switch
    {
        ExecutionMode.Preview => new QualityScalingPolicy(0.35f, 1.5f, 0),
        ExecutionMode.Standard => new QualityScalingPolicy(0.7f, 1.15f, 1),
        ExecutionMode.Final => new QualityScalingPolicy(1.0f, 1.0f, 2),
        _ => new QualityScalingPolicy(1.0f, 1.0f, 1)
    };

    public QualityScalingPolicy ForOperator(string operatorId)
    {
        if (string.Equals(operatorId, ThicknessEnforceOperator.CanonicalId, StringComparison.OrdinalIgnoreCase))
        {
            return this with { SamplingDensityScale = MathF.Min(1.0f, SamplingDensityScale * 1.1f) };
        }

        if (string.Equals(operatorId, RepairFixOperator.CanonicalId, StringComparison.OrdinalIgnoreCase))
        {
            return this with { SmoothingPasses = Math.Max(0, SmoothingPasses) };
        }

        return this;
    }
}
