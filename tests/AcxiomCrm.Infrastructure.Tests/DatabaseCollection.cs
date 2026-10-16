using AcxiomCrm.TestSupport;

namespace AcxiomCrm.Infrastructure.Tests;

[CollectionDefinition("sql")]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerContainerFixture>
{
}
