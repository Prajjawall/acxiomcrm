using System.Net;
using AcxiomCrm.TestSupport;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class HealthEndpointsTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _factory;

    public HealthEndpointsTests(WebAppFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Live_returns_200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_returns_200_when_database_up()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_returns_503_when_database_unreachable()
    {
        var badFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", "Server=127.0.0.1,9999;Database=Bad;User Id=sa;Password=Wrong;Connect Timeout=1;Encrypt=False;");
            builder.UseSetting("Database:MigrateOnStartup", "false");
        });

        var client = badFactory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}
