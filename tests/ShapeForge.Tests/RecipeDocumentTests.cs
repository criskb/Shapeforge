using ShapeForge.Core.Operators;
using ShapeForge.Core.Pipeline;
using System.Text.Json;

namespace ShapeForge.Tests;

public class RecipeDocumentTests
{
    [Fact]
    public void FromJson_MigratesV1RecipeToV2Schema()
    {
        const string v1Json = """
        {
          "version": 1,
          "units": "mm",
          "steps": [
            {
              "op": "repair.fix",
              "params": {
                "closeRadiusMm": 0.4
              }
            }
          ]
        }
        """;

        var doc = RecipeDocument.FromJson(v1Json);

        Assert.Equal((int)RecipeVersion.V2, doc.Version);
        Assert.Equal("mm", doc.Profile?.Units);
        Assert.Single(doc.Recipe.Steps);
        Assert.Equal("repair.fix", doc.Recipe.Steps[0].Op);
    }
    [Fact]
    public void FromJson_SupportsCanonicalRecipeVersionField()
    {
        const string v2Json = """
        {
          "recipeVersion": 2,
          "profile": { "units": "mm" },
          "recipe": {
            "steps": [
              {
                "op": "repair.fix",
                "params": { "closeRadiusMm": 0.2 }
              }
            ]
          }
        }
        """;

        var doc = RecipeDocument.FromJson(v2Json);

        Assert.Equal(RecipeDocument.CurrentVersion, doc.Version);
        Assert.Equal("repair.fix", doc.Recipe.Steps[0].Op);
    }

    [Fact]
    public void FromJson_RejectsUnsupportedRecipeVersion()
    {
        const string unsupported = """
        {
          "recipeVersion": 3,
          "recipe": { "steps": [] }
        }
        """;

        var ex = Assert.Throws<InvalidOperationException>(() => RecipeDocument.FromJson(unsupported));
        Assert.Contains("Unsupported recipe version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void ResolveEffectiveProfile_AppliesExpectedInheritanceOrder()
    {
        var baseProfile = Presets.Resolve(PrintPreset.Fdm);
        var doc = RecipeDocument.CreateV2(
            profile: new ProfileDocument(Units: "mm", MinWallMm: 1.0f),
            recipe: new RecipeDefinition([]),
            pem: new PemDocument(
                Name: "sla-safe",
                Defaults: new ProfileDocument(Mode: ProcessMode.Resin, MinWallMm: 0.85f),
                Recipe: new RecipeDefinition([])));

        var runtime = new ProfileDocument(Units: "in", MinWallMm: 0.95f, Quality: PresetQuality.Preview);
        var resolved = doc.ResolveEffectiveProfile(baseProfile, runtime);

        Assert.Equal(ProcessMode.Resin, resolved.Mode);
        Assert.Equal(PresetQuality.Preview, resolved.Quality);
        Assert.Equal("in", resolved.Units);
        Assert.Equal(0.95f, resolved.MinWallMm);
    }

    [Fact]
    public void Validate_ReturnsHelpfulErrorsForUnknownOperatorRangeAndUnits()
    {
        var registry = new OperatorRegistry();
        registry.Register(new RepairFixOperator());
        registry.Register(new ThicknessEnforceOperator(1.2f, ThicknessMode.Inflate));

        var recipe = new RecipeDocument(
            Version: (int)RecipeVersion.V2,
            Profile: new ProfileDocument(Units: "in"),
            Recipe: new RecipeDefinition(
            [
                new RecipeStep("unknown.op", new Dictionary<string, JsonElement>()),
                new RecipeStep("repair.fix", new Dictionary<string, JsonElement>
                {
                    ["closeRadiusMm"] = JsonSerializer.SerializeToElement(-1.0),
                    ["unknownParam"] = JsonSerializer.SerializeToElement(10)
                })
            ]),
            Pem: new PemDocument(
                Name: "strict",
                Defaults: null,
                Recipe: new RecipeDefinition([]),
                Validation: new ValidationRuleSet(AllowedUnits: ["mm", "in"])));

        var errors = recipe.Validate(registry);

        Assert.Contains(errors, e => e.Contains("unknown operator id 'unknown.op'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("below minimum", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("unknown parameter 'unknownParam'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("expressed in millimeters", StringComparison.OrdinalIgnoreCase));
    }
}
