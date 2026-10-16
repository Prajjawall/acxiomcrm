using System.Net;
using System.Security.Claims;
using AcxiomCrm.Web.Services;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class HttpCurrentUserTests
{
    [Fact]
    public void Reads_claims_and_ip()
    {
        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Name, "jane.doe"),
            new Claim(ClaimTypes.Role, "Manager")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
        context.Items["CorrelationId"] = "corr-test-123";

        var accessor = new HttpContextAccessor { HttpContext = context };
        var currentUser = new HttpCurrentUser(accessor);

        currentUser.IsAuthenticated.ShouldBeTrue();
        currentUser.UserId.ShouldBe("user-123");
        currentUser.UserName.ShouldBe("jane.doe");
        currentUser.Role.ShouldBe("Manager");
        currentUser.IpAddress.ShouldBe("10.20.30.40");
        currentUser.CorrelationId.ShouldBe("corr-test-123");
    }

    [Fact]
    public void Anonymous_has_null_ids_and_IsAuthenticated_false()
    {
        var context = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = context };
        var currentUser = new HttpCurrentUser(accessor);

        currentUser.IsAuthenticated.ShouldBeFalse();
        currentUser.UserId.ShouldBeNull();
        currentUser.UserName.ShouldBeNull();
        currentUser.Role.ShouldBeNull();
    }
}
