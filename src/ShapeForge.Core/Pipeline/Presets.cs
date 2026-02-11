namespace ShapeForge.Core.Pipeline;

public enum PrintPreset
{
    Fdm,
    Sla,
    Sls
}

public record PresetParameters(float VoxelSizeMm, float MinWallMm, float MinimumDrainHoleMm);

public static class Presets
{
    public static PresetParameters Resolve(PrintPreset preset) => preset switch
    {
        PrintPreset.Fdm => new(0.2f, 1.2f, 2.0f),
        PrintPreset.Sla => new(0.05f, 0.8f, 2.0f),
        PrintPreset.Sls => new(0.1f, 0.7f, 1.5f),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
    };
}
