using System.ComponentModel.DataAnnotations;
using AcxiomCrm.Application.Abstractions;
using AcxiomCrm.Domain.Validation;
using AcxiomCrm.TestSupport;
using AcxiomCrm.Web.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class ValidationAttributesTests
{
    private sealed class TestModel
    {
        [AxRequired("Customer name")]
        [AxMaxLength(Limits.Name, "Customer name")]
        public string? Name { get; set; }

        [AxEmail]
        public string? Email { get; set; }

        [AxPhone]
        public string? Phone { get; set; }

        [AxFutureDate("Expected Close Date")]
        public DateOnly? ExpectedCloseDate { get; set; }

        [AxFutureDate("Follow-up date", ErrorMessage = "Follow-up date cannot be earlier than today.")]
        public DateOnly? FollowUpDate { get; set; }

        [AxRange(0, 100, "Probability")]
        public int? Probability { get; set; }
    }

    [Fact]
    public void AxRequired_returns_canonical_message_when_value_is_null_or_whitespace()
    {
        var attr = new AxRequiredAttribute("Customer name");

        attr.IsValid(null).ShouldBeFalse();
        attr.IsValid("").ShouldBeFalse();
        attr.IsValid("   ").ShouldBeFalse();
        attr.FormatErrorMessage("Name").ShouldBe("Customer name is required.");

        attr.IsValid("Acme Corp").ShouldBeTrue();
    }

    [Fact]
    public void AxMaxLength_returns_canonical_message_when_value_exceeds_length()
    {
        var attr = new AxMaxLengthAttribute(Limits.Name, "Customer name");

        attr.IsValid(new string('a', 151)).ShouldBeFalse();
        attr.FormatErrorMessage("Name").ShouldBe("Customer name must not exceed 150 characters.");

        attr.IsValid(new string('a', 150)).ShouldBeTrue();
        attr.IsValid(null).ShouldBeTrue();
    }

    [Fact]
    public void AxEmail_returns_canonical_message_on_invalid_email()
    {
        var attr = new AxEmailAttribute();

        attr.IsValid("not-an-email").ShouldBeFalse();
        attr.IsValid("user@").ShouldBeFalse();
        attr.IsValid("@example.com").ShouldBeFalse();
        attr.FormatErrorMessage("Email").ShouldBe("Enter a valid email address.");

        attr.IsValid("user@example.com").ShouldBeTrue();
        attr.IsValid(null).ShouldBeTrue();
        attr.IsValid("").ShouldBeTrue();
    }

    [Fact]
    public void AxPhone_returns_canonical_message_on_invalid_phone()
    {
        var attr = new AxPhoneAttribute();

        attr.IsValid("12345").ShouldBeFalse();
        attr.IsValid("5555555555").ShouldBeFalse(); // Starts with 5, Indian phones start 6-9
        attr.FormatErrorMessage("Phone").ShouldBe("Enter a valid phone number.");

        attr.IsValid("9876543210").ShouldBeTrue();
        attr.IsValid("+91 98765-43210").ShouldBeTrue();
        attr.IsValid(null).ShouldBeTrue();
        attr.IsValid("").ShouldBeTrue();
    }

    [Fact]
    public void AxRange_returns_canonical_message_on_out_of_range()
    {
        var attr = new AxRangeAttribute(0, 100, "Probability");

        attr.IsValid(-1).ShouldBeFalse();
        attr.IsValid(101).ShouldBeFalse();
        attr.FormatErrorMessage("Probability").ShouldBe("Probability must be between 0 and 100.");

        attr.IsValid(0).ShouldBeTrue();
        attr.IsValid(50).ShouldBeTrue();
        attr.IsValid(100).ShouldBeTrue();
    }

    [Fact]
    public void AxFutureDate_validates_against_IClock()
    {
        var fakeToday = new DateOnly(2026, 10, 17);
        var clock = new FakeClock(new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc));

        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);
        var serviceProvider = services.BuildServiceProvider();

        var attr = new AxFutureDateAttribute("Expected Close Date");

        var yesterdayContext = new ValidationContext(new object(), serviceProvider, null)
        {
            MemberName = "ExpectedCloseDate",
            DisplayName = "Expected Close Date"
        };
        var resultYesterday = attr.GetValidationResult(fakeToday.AddDays(-1), yesterdayContext);
        resultYesterday.ShouldNotBeNull();
        resultYesterday.ErrorMessage.ShouldBe("Expected Close Date cannot be in the past.");

        var todayContext = new ValidationContext(new object(), serviceProvider, null)
        {
            MemberName = "ExpectedCloseDate"
        };
        var resultToday = attr.GetValidationResult(fakeToday, todayContext);
        resultToday.ShouldBe(ValidationResult.Success);

        var tomorrowContext = new ValidationContext(new object(), serviceProvider, null)
        {
            MemberName = "ExpectedCloseDate"
        };
        var resultTomorrow = attr.GetValidationResult(fakeToday.AddDays(1), tomorrowContext);
        resultTomorrow.ShouldBe(ValidationResult.Success);
    }

    [Fact]
    public void AxFutureDate_custom_message_is_honoured()
    {
        var fakeToday = new DateOnly(2026, 10, 17);
        var clock = new FakeClock(new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc));

        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);
        var serviceProvider = services.BuildServiceProvider();

        var attr = new AxFutureDateAttribute("Follow-up date")
        {
            ErrorMessage = "Follow-up date cannot be earlier than today."
        };

        var context = new ValidationContext(new object(), serviceProvider, null)
        {
            MemberName = "FollowUpDate"
        };
        var result = attr.GetValidationResult(fakeToday.AddDays(-1), context);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Follow-up date cannot be earlier than today.");
    }

    [Fact]
    public void Adapter_provider_emits_expected_client_validation_attributes()
    {
        var clock = new FakeClock(new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc));
        var services = new ServiceCollection();
        services.AddSingleton<IClock>(clock);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = sp };
        var actionContext = new ActionContext(httpContext, new(), new());
        var metadataProvider = new EmptyModelMetadataProvider();
        var modelMetadata = metadataProvider.GetMetadataForProperty(typeof(TestModel), nameof(TestModel.Name));

        var provider = new AxAttributeAdapterProvider();

        // 1. AxRequired
        var reqAttr = new AxRequiredAttribute("Customer name");
        var reqAdapter = provider.GetAttributeAdapter(reqAttr, null);
        reqAdapter.ShouldNotBeNull();
        var reqAttrs = new Dictionary<string, string>();
        var reqContext = new ClientModelValidationContext(actionContext, modelMetadata, metadataProvider, reqAttrs);
        reqAdapter.AddValidation(reqContext);
        reqAttrs["data-val"].ShouldBe("true");
        reqAttrs["data-val-required"].ShouldBe("Customer name is required.");

        // 2. AxMaxLength
        var maxAttr = new AxMaxLengthAttribute(150, "Customer name");
        var maxAdapter = provider.GetAttributeAdapter(maxAttr, null);
        maxAdapter.ShouldNotBeNull();
        var maxAttrs = new Dictionary<string, string>();
        var maxContext = new ClientModelValidationContext(actionContext, modelMetadata, metadataProvider, maxAttrs);
        maxAdapter.AddValidation(maxContext);
        maxAttrs["data-val"].ShouldBe("true");
        maxAttrs["data-val-length"].ShouldBe("Customer name must not exceed 150 characters.");
        maxAttrs["data-val-length-max"].ShouldBe("150");

        // 3. AxEmail
        var emailAttr = new AxEmailAttribute();
        var emailAdapter = provider.GetAttributeAdapter(emailAttr, null);
        emailAdapter.ShouldNotBeNull();
        var emailAttrs = new Dictionary<string, string>();
        var emailContext = new ClientModelValidationContext(actionContext, modelMetadata, metadataProvider, emailAttrs);
        emailAdapter.AddValidation(emailContext);
        emailAttrs["data-val"].ShouldBe("true");
        emailAttrs["data-val-axemail"].ShouldBe("Enter a valid email address.");

        // 4. AxPhone
        var phoneAttr = new AxPhoneAttribute();
        var phoneAdapter = provider.GetAttributeAdapter(phoneAttr, null);
        phoneAdapter.ShouldNotBeNull();
        var phoneAttrs = new Dictionary<string, string>();
        var phoneContext = new ClientModelValidationContext(actionContext, modelMetadata, metadataProvider, phoneAttrs);
        phoneAdapter.AddValidation(phoneContext);
        phoneAttrs["data-val"].ShouldBe("true");
        phoneAttrs["data-val-axphone"].ShouldBe("Enter a valid phone number.");

        // 5. AxFutureDate
        var futureAttr = new AxFutureDateAttribute("Expected Close Date");
        var futureAdapter = provider.GetAttributeAdapter(futureAttr, null);
        futureAdapter.ShouldNotBeNull();
        var futureAttrs = new Dictionary<string, string>();
        var futureContext = new ClientModelValidationContext(actionContext, modelMetadata, metadataProvider, futureAttrs);
        futureAdapter.AddValidation(futureContext);
        futureAttrs["data-val"].ShouldBe("true");
        futureAttrs["data-val-axfuturedate"].ShouldBe("Expected Close Date cannot be in the past.");
        futureAttrs["data-val-axfuturedate-today"].ShouldBe("2026-10-17");
    }
}
