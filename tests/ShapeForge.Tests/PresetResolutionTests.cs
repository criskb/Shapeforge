using ShapeForge.Core.Pipeline;

namespace ShapeForge.Tests;

public class PresetResolutionTests
{
    [Theory]
    [InlineData("Fdm", PrintPreset.Fdm)]
    [InlineData("sla", PrintPreset.Sla)]
    [InlineData("Resin", PrintPreset.Sla)]
    [InlineData("SLS", PrintPreset.Sls)]
    public void TryParsePreset_SupportsLegacyAndAliasValues(string raw, PrintPreset expected)
    {
        var parsed = Presets.TryParsePreset(raw, out var preset);

        Assert.True(parsed);
        Assert.Equal(expected, preset);
    }

    [Fact]
    public void Resolve_AllowsModeQualityAndUnitsOverrides()
    {
        var profile = Presets.Resolve(
            preset: PrintPreset.Fdm,
            unitsOverride: "in",
            modeOverride: ProcessMode.Resin,
            qualityOverride: PresetQuality.Preview,
            repairModeOverride: RepairMode.Aggressive);

        Assert.Equal(ProcessMode.Resin, profile.Mode);
        Assert.Equal(PresetQuality.Preview, profile.Quality);
        Assert.Equal("in", profile.Units);
        Assert.Equal(0.08f, profile.VoxelSizeMm);
        Assert.Equal(0.9f, profile.MinWallMm);
        Assert.Equal(MinimumWallPolicy.Adaptive, profile.MinWallPolicy);
        Assert.Equal(35f, profile.OverhangThresholdDeg);
        Assert.Equal(RepairMode.Aggressive, profile.RepairMode);
    }
}
