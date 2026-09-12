namespace TaskFlow.Web.Infrastructure;

public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("Email (sin servicio SMTP): para {To}, asunto {Subject}. Contenido: {Body}", to, subject, body);
        return Task.CompletedTask;
    }
}