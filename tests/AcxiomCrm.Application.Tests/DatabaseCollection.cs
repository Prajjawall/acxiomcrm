using AcxiomCrm.TestSupport;

namespace AcxiomCrm.Application.Tests;

[CollectionDefinition("sql")]
public sealed class DatabaseCollection : ICollectionFixture<SqlServerContainerFixture>
{
}
