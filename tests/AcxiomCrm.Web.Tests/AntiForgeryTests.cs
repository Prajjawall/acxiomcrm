using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class AntiForgeryTests
{
    [Fact]
    public void Auto_antiforgery_filter_is_registered_in_mvc_options()
    {
        var services = new ServiceCollection();
        services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });

        var sp = services.BuildServiceProvider();
        var mvcOptions = sp.GetRequiredService<IOptions<MvcOptions>>().Value;

        mvcOptions.Filters.ShouldContain(f => f is AutoValidateAntiforgeryTokenAttribute);
    }
}
