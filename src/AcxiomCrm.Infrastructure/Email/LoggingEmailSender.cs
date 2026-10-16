using AcxiomCrm.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AcxiomCrm.Infrastructure.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Sending email to {ToEmail} with subject '{Subject}'", toEmail, subject);
        }

        return Task.CompletedTask;
    }
}
