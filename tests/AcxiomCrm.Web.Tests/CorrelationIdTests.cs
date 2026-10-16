using System.Text.RegularExpressions;
using AcxiomCrm.TestSupport;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class CorrelationIdTests : IClassFixture<WebAppFixture>
{
    private readonly WebAppFixture _factory;

    public CorrelationIdTests(WebAppFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Generates_when_absent()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        response.Headers.Contains("X-Correlation-Id").ShouldBeTrue();
        var correlationId = response.Headers.GetValues("X-Correlation-Id").Single();
        correlationId.Length.ShouldBe(32);
        Regex.IsMatch(correlationId, "^[a-f0-9]{32}$").ShouldBeTrue();
    }

    [Fact]
    public async Task Echoes_valid_incoming_header()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Correlation-Id", "custom-request-id-123");

        var response = await client.SendAsync(request);

        response.Headers.Contains("X-Correlation-Id").ShouldBeTrue();
        var correlationId = response.Headers.GetValues("X-Correlation-Id").Single();
        correlationId.ShouldBe("custom-request-id-123");
    }

    [Fact]
    public async Task Replaces_header_longer_than_64_chars_or_with_control_characters()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        var overlyLongId = new string('x', 65);
        request.Headers.Add("X-Correlation-Id", overlyLongId);

        var response = await client.SendAsync(request);

        response.Headers.Contains("X-Correlation-Id").ShouldBeTrue();
        var correlationId = response.Headers.GetValues("X-Correlation-Id").Single();
        correlationId.ShouldNotBe(overlyLongId);
        correlationId.Length.ShouldBe(32);
        Regex.IsMatch(correlationId, "^[a-f0-9]{32}$").ShouldBeTrue();
    }
}
