using System;

namespace ShapeForge.Core.Operators;

public static class OperatorCategories
{
    public const string RepairMesh = "repair.mesh";
    public const string AnalysisMesh = "analysis.mesh";
    public const string PrepFdmThickness = "prep.fdm.thickness";
    public const string PrepResinSupport = "prep.resin.support";
    public const string TransformGeometry = "transform.geometry";
    public const string ExportMesh = "export.mesh";
}

public static class DeprecatedOperatorIds
{
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["thickness.enforce"] = ThicknessEnforceOperator.CanonicalId
    };
}
