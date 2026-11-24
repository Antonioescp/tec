using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Core.Services;

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();
    public DateTimeOffset Now => DateTimeOffset.Now;
}

/// <summary>Accepts only interactions from the given processes.</summary>
public sealed class ProcessInteractionFilter(IEnumerable<int> processIds) : IInteractionFilter
{
    private readonly HashSet<int> _processIds = [.. processIds];

    public bool ShouldCapture(Interaction interaction) =>
        interaction.ProcessId is { } pid && _processIds.Contains(pid);
}

/// <summary>Accepts only interactions whose kind is enabled.</summary>
public sealed class KindInteractionFilter(IEnumerable<InteractionKind> enabledKinds) : IInteractionFilter
{
    private readonly HashSet<InteractionKind> _kinds = [.. enabledKinds];

    public bool ShouldCapture(Interaction interaction) => _kinds.Contains(interaction.Kind);
}

/// <summary>Accepts an interaction only if every filter accepts it.</summary>
public sealed class AllInteractionFilters(IEnumerable<IInteractionFilter> filters) : IInteractionFilter
{
    private readonly IReadOnlyList<IInteractionFilter> _filters = [.. filters];

    public bool ShouldCapture(Interaction interaction) => _filters.All(f => f.ShouldCapture(interaction));
}
