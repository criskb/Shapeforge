using ShapeForge.Core.Operators;

namespace ShapeForge.Core.Pipeline;

public sealed class OperatorRegistry
{
    private readonly Dictionary<string, IOperator> _operators = new(StringComparer.OrdinalIgnoreCase);

    public void Register(IOperator op) => _operators[op.Id] = op;

    public IReadOnlyCollection<IOperator> List() => _operators.Values.OrderBy(o => o.Id).ToArray();

    public bool TryGet(string id, out IOperator? op) => _operators.TryGetValue(id, out op);
}
