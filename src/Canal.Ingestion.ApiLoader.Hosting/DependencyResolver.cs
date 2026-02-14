using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Hosting;

/// <summary>
/// Resolves endpoint dependency chains within a single vendor's endpoint list.
/// Migrated from the deleted <c>EndpointRegistry.ResolveDependencyChain</c>.
/// </summary>
/// <remarks>
/// Only linear single-parent chains are supported (A → B → C). Each endpoint may
/// have at most one <see cref="EndpointDefinition.DependsOn"/> value. Diamond or
/// multi-parent graphs are not supported.
/// </remarks>
internal static class DependencyResolver
{
    /// <summary>
    /// Returns the dependency chain for <paramref name="target"/> in execution order
    /// (dependencies first, target last). Uses <see cref="EndpointDefinition.DependsOn"/>
    /// to walk the chain.
    /// </summary>
    /// <param name="target">The endpoint the user wants to load.</param>
    /// <param name="allEndpoints">The vendor's full endpoint catalog.</param>
    /// <returns>Ordered list: [deepest dependency, ..., target].</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown on circular dependency or if a referenced <c>DependsOn</c> name is not found
    /// in <paramref name="allEndpoints"/>.
    /// </exception>
    public static List<EndpointEntry> Resolve(
        EndpointEntry target,
        IReadOnlyList<EndpointEntry> allEndpoints)
    {
        // O(1) lookup by name for dependency resolution
        var lookup = new Dictionary<string, EndpointEntry>(allEndpoints.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var ep in allEndpoints)
            lookup[ep.Name] = ep;

        var chain = new List<EndpointEntry>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = target;

        while (current is not null)
        {
            if (!visited.Add(current.Name))
                throw new InvalidOperationException(
                    $"Circular dependency detected at '{current.Name}'.");

            chain.Add(current);

            var dependsOn = current.Definition.DependsOn;

            // Guard: an endpoint that requires an iteration list must declare a dependency to provide it
            if (current.Definition.RequiresIterationList && dependsOn is null)
                throw new InvalidOperationException(
                    $"Endpoint '{current.Name}' requires an iteration list (RequiresIterationList=true) but declares no DependsOn.");

            if (dependsOn is null)
                break;

            current = lookup.TryGetValue(dependsOn, out var dep)
                ? dep
                : throw new InvalidOperationException(
                    $"Dependency '{dependsOn}' not found for endpoint '{current.Name}'.");
        }

        chain.Reverse();
        return chain;
    }
}
