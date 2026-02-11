using ShapeForge.Core.Geometry;
using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Diagnostics;

public static class ReportCard
{
    public static Dictionary<string, double> Build(MeshModel before, MeshModel after, IEnumerable<OpReport> reports)
    {
        var dict = new Dictionary<string, double>
        {
            ["triangles.before"] = MeshMetrics.TriangleCount(before),
            ["triangles.after"] = MeshMetrics.TriangleCount(after)
        };

        foreach (var report in reports)
        {
            foreach (var metric in report.Metrics)
            {
                dict[$"{report.Name}.{metric.Key}"] = metric.Value;
            }
        }

        return dict;
    }
}
