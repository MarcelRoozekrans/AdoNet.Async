using Xunit;

namespace System.Data.Async.Validation.Tests.Infrastructure;

[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "xUnit collection fixture convention")]
public sealed class ValidationCollection : ICollectionFixture<ValidationFixture>
{
    public const string Name = "Validation";
}
