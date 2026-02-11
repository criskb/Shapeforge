using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Pipeline;

public enum PrintPreset
{
    Fdm,
    Sla,
    Sls
}

public enum ProcessMode
{
    Fdm,
    Resin,
    Sls
}

public enum PresetQuality
{
    Preview,
    Final
}

public enum MinimumWallPolicy
{
    Strict,
    Adaptive
}

public enum RepairMode
{
    Conservative,
    Balanced,
    Aggressive
}

public record PresetParameters(
    ProcessMode Mode,
    PresetQuality Quality,
    string Units,
    float VoxelSizeMm,
    float MinWallMm,
    MinimumWallPolicy MinWallPolicy,
    float OverhangThresholdDeg,
    float MinimumDrainHoleMm,
    ThicknessMode ThicknessMode,
    RepairMode RepairMode);

public static class Presets
{
    public static bool TryParsePreset(string raw, out PrintPreset preset)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "fdm":
                preset = PrintPreset.Fdm;
                return true;
            case "sla":
            case "resin":
                preset = PrintPreset.Sla;
                return true;
            case "sls":
                preset = PrintPreset.Sls;
                return true;
            default:
                preset = default;
                return false;
        }
    }

    public static bool TryParseMode(string raw, out ProcessMode mode)
    {
        switch (raw.Trim().ToLowerInvariant())
        {
            case "fdm":
                mode = ProcessMode.Fdm;
                return true;
            case "sla":
            case "resin":
                mode = ProcessMode.Resin;
                return true;
            case "sls":
                mode = ProcessMode.Sls;
                return true;
            default:
                mode = default;
                return false;
        }
    }

    public static bool TryParseQuality(string raw, out PresetQuality quality)
        => Enum.TryParse(raw, ignoreCase: true, out quality);

    public static bool TryParseRepairMode(string raw, out RepairMode repairMode)
        => Enum.TryParse(raw, ignoreCase: true, out repairMode);

    public static PresetParameters Resolve(PrintPreset preset) => preset switch
    {
        PrintPreset.Fdm => BuildProfile(ProcessMode.Fdm, PresetQuality.Final, "mm"),
        PrintPreset.Sla => BuildProfile(ProcessMode.Resin, PresetQuality.Final, "mm"),
        PrintPreset.Sls => BuildProfile(ProcessMode.Sls, PresetQuality.Final, "mm"),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null)
    };

    public static PresetParameters Resolve(
        PrintPreset preset,
        string? unitsOverride,
        ProcessMode? modeOverride,
        PresetQuality? qualityOverride,
        RepairMode? repairModeOverride)
    {
        var baseline = Resolve(preset);
        var mode = modeOverride ?? baseline.Mode;
        var quality = qualityOverride ?? baseline.Quality;
        var units = string.IsNullOrWhiteSpace(unitsOverride) ? baseline.Units : unitsOverride.Trim();

        var profile = BuildProfile(mode, quality, units);
        if (repairModeOverride is not null)
        {
            profile = profile with { RepairMode = repairModeOverride.Value };
        }

        return profile;
    }

    private static PresetParameters BuildProfile(ProcessMode mode, PresetQuality quality, string units)
    {
        return (mode, quality) switch
        {
            (ProcessMode.Fdm, PresetQuality.Preview) => new(
                mode,
                quality,
                units,
                VoxelSizeMm: 0.28f,
                MinWallMm: 1.4f,
                MinWallPolicy: MinimumWallPolicy.Adaptive,
                OverhangThresholdDeg: 50f,
                MinimumDrainHoleMm: 0f,
                ThicknessMode: ThicknessMode.Inflate,
                RepairMode: RepairMode.Conservative),
            (ProcessMode.Fdm, PresetQuality.Final) => new(
                mode,
                quality,
                units,
                VoxelSizeMm: 0.2f,
                MinWallMm: 1.2f,
                MinWallPolicy: MinimumWallPolicy.Strict,
                OverhangThresholdDeg: 45f,
                MinimumDrainHoleMm: 0f,
                ThicknessMode: ThicknessMode.Inflate,
                RepairMode: RepairMode.Balanced),
            (ProcessMode.Resin, PresetQuality.Preview) => new(
                mode,
                quality,
                units,
                VoxelSizeMm: 0.08f,
                MinWallMm: 0.9f,
                MinWallPolicy: MinimumWallPolicy.Adaptive,
                OverhangThresholdDeg: 35f,
                MinimumDrainHoleMm: 2.2f,
                ThicknessMode: ThicknessMode.Inflate,
                RepairMode: RepairMode.Balanced),
            (ProcessMode.Resin, PresetQuality.Final) => new(
                mode,
                quality,
                units,
                VoxelSizeMm: 0.05f,
                MinWallMm: 0.8f,
                MinWallPolicy: MinimumWallPolicy.Strict,
                OverhangThresholdDeg: 30f,
                MinimumDrainHoleMm: 2.0f,
                ThicknessMode: ThicknessMode.Inflate,
                RepairMode: RepairMode.Aggressive),
            (ProcessMode.Sls, PresetQuality.Preview) => new(
                mode,
                quality,
                units,
                VoxelSizeMm: 0.14f,
                MinWallMm: 0.8f,
                MinWallPolicy: MinimumWallPolicy.Adaptive,
                OverhangThresholdDeg: 60f,
                MinimumDrainHoleMm: 1.8f,
                ThicknessMode: ThicknessMode.Reshell,
                RepairMode: RepairMode.Conservative),
            (ProcessMode.Sls, PresetQuality.Final) => new(
                mode,
                quality,
                units,
                VoxelSizeMm: 0.1f,
                MinWallMm: 0.7f,
                MinWallPolicy: MinimumWallPolicy.Strict,
                OverhangThresholdDeg: 55f,
                MinimumDrainHoleMm: 1.5f,
                ThicknessMode: ThicknessMode.Reshell,
                RepairMode: RepairMode.Balanced),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
}
