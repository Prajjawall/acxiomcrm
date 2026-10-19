using System.Globalization;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.TestSupport;
using AcxiomCrm.Web.Validation;
using AcxiomCrm.Web.ViewModels.Opportunities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class OpportunityClientValidationMarkupTests
{
    [Fact]
    public void Form_inputs_carry_data_val_attributes_for_range_and_futuredate()
    {
        var fixedDate = new DateOnly(2026, 10, 18);
        var clock = new FakeClock(new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc));
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var actionContext = new ActionContext(httpContext, new(), new());
        var metadataProvider = new EmptyModelMetadataProvider();
        var provider = new AxAttributeAdapterProvider();

        // 1. ExpectedCloseDate carries AxFutureDate with today's date injected
        var datePropMetadata = metadataProvider.GetMetadataForProperty(typeof(OpportunityFormViewModel), nameof(OpportunityFormViewModel.ExpectedCloseDate));
        var dateAttr = new AxFutureDateAttribute("Expected Close Date");
        var dateAdapter = provider.GetAttributeAdapter(dateAttr, null);
        dateAdapter.ShouldNotBeNull();

        var dateAttrs = new Dictionary<string, string>();
        var dateContext = new ClientModelValidationContext(actionContext, datePropMetadata, metadataProvider, dateAttrs);
        dateAdapter.AddValidation(dateContext);

        dateAttrs["data-val"].ShouldBe("true");
        dateAttrs["data-val-axfuturedate"].ShouldBe("Expected Close Date cannot be in the past.");
        dateAttrs["data-val-axfuturedate-today"].ShouldBe(fixedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        // 2. Probability carries AxRange
        var probPropMetadata = metadataProvider.GetMetadataForProperty(typeof(OpportunityFormViewModel), nameof(OpportunityFormViewModel.Probability));
        var rangeAttr = new AxRangeAttribute(0, 100, "Opportunity Probability");
        var rangeAdapter = provider.GetAttributeAdapter(rangeAttr, null);
        rangeAdapter.ShouldNotBeNull();

        var rangeAttrs = new Dictionary<string, string>();
        var rangeContext = new ClientModelValidationContext(actionContext, probPropMetadata, metadataProvider, rangeAttrs);
        rangeAdapter.AddValidation(rangeContext);

        rangeAttrs["data-val"].ShouldBe("true");
        rangeAttrs["data-val-range"].ShouldBe("Opportunity Probability must be between 0 and 100.");
        rangeAttrs["data-val-range-min"].ShouldBe("0");
        rangeAttrs["data-val-range-max"].ShouldBe("100");
    }
}
