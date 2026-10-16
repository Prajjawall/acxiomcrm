using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Common;
using Shouldly;

namespace AcxiomCrm.Application.Tests;

public class OwnerScopeTests
{
    private sealed class TestOwnedEntity(string ownerId) : IOwnedEntity
    {
        public string OwnerId { get; } = ownerId;
    }

    [Fact]
    public void InScope_all_returns_everything()
    {
        var entities = new List<TestOwnedEntity>
        {
            new("user-1"),
            new("user-2"),
            new("user-3")
        }.AsQueryable();

        var scope = new OwnerScope(All: true, []);
        var result = entities.InScope(scope).ToList();

        result.Count.ShouldBe(3);
    }

    [Fact]
    public void InScope_filters_by_owner_ids()
    {
        var entities = new List<TestOwnedEntity>
        {
            new("user-1"),
            new("user-2"),
            new("user-3")
        }.AsQueryable();

        var scope = new OwnerScope(All: false, ["user-1", "user-3"]);
        var result = entities.InScope(scope).ToList();

        result.Count.ShouldBe(2);
        result.Select(x => x.OwnerId).ShouldBe(["user-1", "user-3"]);
    }

    [Fact]
    public void InScope_empty_ids_returns_nothing()
    {
        var entities = new List<TestOwnedEntity>
        {
            new("user-1"),
            new("user-2")
        }.AsQueryable();

        var scope = new OwnerScope(All: false, []);
        var result = entities.InScope(scope).ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Contains_matches_case_sensitively_on_ids()
    {
        var scope = new OwnerScope(All: false, ["User-1"]);

        scope.Contains("User-1").ShouldBeTrue();
        scope.Contains("user-1").ShouldBeFalse();
        scope.Contains("User-2").ShouldBeFalse();
    }
}
