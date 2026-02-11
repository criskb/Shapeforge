using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Pipeline;

public sealed class OperatorRegistry
{
    private readonly Dictionary<string, IOperator> _operators = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _deprecatedIds = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IOperator op)
    {
        _operators[op.Id] = op;
    }

    public void RegisterCompatibilityMap(IReadOnlyDictionary<string, string> oldToNew)
    {
        foreach (var (oldId, newId) in oldToNew)
        {
            _deprecatedIds[oldId] = newId;
        }
    }

    public IReadOnlyCollection<IOperator> List() => _operators.Values.OrderBy(o => o.Id).ToArray();

    public IReadOnlyDictionary<string, string> CompatibilityMap => _deprecatedIds;

    public bool TryGet(string id, out IOperator? op)
    {
        if (_operators.TryGetValue(id, out op))
        {
            return true;
        }

        if (_deprecatedIds.TryGetValue(id, out var newId) && _operators.TryGetValue(newId, out op))
        {
            return true;
        }

        op = null;
        return false;
    }

    public string ResolveCanonicalId(string id)
        => _deprecatedIds.TryGetValue(id, out var newId) ? newId : id;
}
