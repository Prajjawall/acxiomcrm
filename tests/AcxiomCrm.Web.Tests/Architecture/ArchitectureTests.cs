using System.Reflection;
using NetArchTest.Rules;
using Shouldly;

namespace AcxiomCrm.Web.Tests.Architecture;

public class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = Assembly.Load("AcxiomCrm.Domain");
    private static readonly Assembly ApplicationAssembly = Assembly.Load("AcxiomCrm.Application");
    private static readonly Assembly InfrastructureAssembly = Assembly.Load("AcxiomCrm.Infrastructure");
    private static readonly Assembly WebAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_references_no_project_or_framework_beyond_BCL()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "AcxiomCrm.Application",
                "AcxiomCrm.Infrastructure",
                "AcxiomCrm.Web")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_Web()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "AcxiomCrm.Infrastructure",
                "AcxiomCrm.Web")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Infrastructure_does_not_reference_Web()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("AcxiomCrm.Web")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Web_controllers_do_not_use_DbContext_types()
    {
        var result = Types.InAssembly(WebAssembly)
            .That()
            .ResideInNamespaceStartingWith("AcxiomCrm.Web.Controllers")
            .ShouldNot()
            .HaveDependencyOn("AcxiomCrm.Infrastructure.Persistence")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(string.Join(", ", result.FailingTypeNames ?? []));
    }
}
