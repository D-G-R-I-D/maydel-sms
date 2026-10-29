using Maydel.Application.Common;
using Maydel.Domain.Common;

namespace Maydel.Api.Tests.Infrastructure;

/// <summary>
/// The OpenAPI contract names schemas by type name only, so two public types with the same name (in different
/// namespaces) silently share one schema and the typed frontend client gets the wrong shape.
/// </summary>
public sealed class ContractNameTests
{
    [Fact]
    public void Public_application_and_domain_types_have_unique_names()
    {
        var clashes = new[] { typeof(Result).Assembly, typeof(Entity).Assembly }
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !t.IsNested)
            .GroupBy(t => t.Name)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.Namespace))}")
            .ToList();

        Assert.True(clashes.Count == 0, "Rename one of each: " + string.Join("; ", clashes));
    }
}
