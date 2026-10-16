using AcxiomCrm.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class FlashTests
{
    private sealed class MemoryTempDataDictionary : Dictionary<string, object?>, ITempDataDictionary
    {
        public void Load() { }
        public void Save() { }
        public void Keep() { }
        public void Keep(string key) { }
        public object? Peek(string key) => TryGetValue(key, out var val) ? val : null;
    }

    [Fact]
    public void Flash_messages_are_persisted_and_drained_once()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var tempData = new MemoryTempDataDictionary();
        var factory = Substitute.For<ITempDataDictionaryFactory>();
        factory.GetTempData(httpContext).Returns(tempData);

        var flash = new TempDataFlash(accessor, factory);

        flash.Success("Record created successfully.");
        flash.Error("Failed to update record.");

        var drained = flash.Drain();
        drained.Count.ShouldBe(2);
        drained[0].Type.ShouldBe(FlashType.Success);
        drained[0].Message.ShouldBe("Record created successfully.");
        drained[1].Type.ShouldBe(FlashType.Error);
        drained[1].Message.ShouldBe("Failed to update record.");

        // Second drain should be empty
        var secondDrain = flash.Drain();
        secondDrain.ShouldBeEmpty();
    }
}
