using Xunit;

namespace AcxiomCrm.TestSupport;

[CollectionDefinition("sql")]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerContainerFixture>
{
}
