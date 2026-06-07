namespace AISO.AiOrchestration;

/// <summary>
/// Registry of all functions available to the dispatcher.
/// Populated by DI: all <see cref="IFunction"/> registrations are aggregated.
/// </summary>
public interface IFunctionRegistry
{
    IReadOnlyList<IFunction> All { get; }
    IFunction? GetByName(string name);
}

public sealed class FunctionRegistry : IFunctionRegistry
{
    private readonly Dictionary<string, IFunction> _byName;

    public FunctionRegistry(IEnumerable<IFunction> functions)
    {
        _byName = functions.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IFunction> All => _byName.Values.ToList();

    public IFunction? GetByName(string name) =>
        _byName.TryGetValue(name, out var fn) ? fn : null;
}
