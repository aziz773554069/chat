namespace customerplatform.Api.Services;

public interface IWhatsAppService
{
    Task<WhatsAppSendResult> SendTextMessageAsync(
        string phoneNumber,
        string message);
}

public class WhatsAppSendResult
{
    public bool Success { get; set; }

    public string? ExternalMessageId { get; set; }

    public string? ErrorMessage { get; set; }
}