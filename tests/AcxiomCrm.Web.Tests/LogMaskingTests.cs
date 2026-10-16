using AcxiomCrm.Web.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class LogMaskingTests
{
    [Fact]
    public void Masks_sensitive_properties()
    {
        var logEvents = new List<LogEvent>();
        var testSink = new TestLogSink(logEvents);

        var logger = new LoggerConfiguration()
            .Destructure.With<SensitiveDataMaskingPolicy>()
            .WriteTo.Sink(testSink)
            .CreateLogger();

        var payload = new
        {
            UserName = "a",
            Password = "p@ss",
            Token = "secret-token",
            NormalField = "ok"
        };

        logger.Information("Payload: {@Payload}", payload);

        logEvents.Count.ShouldBe(1);
        var logEvent = logEvents[0];

        using var writer = new StringWriter();
        var formatter = new MessageTemplateTextFormatter("{Message:lj}");
        formatter.Format(logEvent, writer);
        var rendered = writer.ToString();

        rendered.ShouldContain("\"UserName\":\"a\"");
        rendered.ShouldContain("\"Password\":\"***\"");
        rendered.ShouldContain("\"Token\":\"***\"");
        rendered.ShouldContain("\"NormalField\":\"ok\"");
        rendered.ShouldNotContain("p@ss");
        rendered.ShouldNotContain("secret-token");
    }

    private sealed class TestLogSink : ILogEventSink
    {
        private readonly List<LogEvent> _events;

        public TestLogSink(List<LogEvent> events)
        {
            _events = events;
        }

        public void Emit(LogEvent logEvent)
        {
            _events.Add(logEvent);
        }
    }
}
