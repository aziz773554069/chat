using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace customerplatform.Api.Services;

public class WhatsAppService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WhatsAppSendResult> SendTextMessageAsync(
        string phoneNumber,
        string message)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return new WhatsAppSendResult
            {
                Success = false,
                ErrorMessage = "رقم هاتف العميل مطلوب."
            };
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return new WhatsAppSendResult
            {
                Success = false,
                ErrorMessage = "محتوى الرسالة مطلوب."
            };
        }

        var accessToken = _configuration["WhatsApp:AccessToken"];

        var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new WhatsAppSendResult
            {
                Success = false,
                ErrorMessage = "WhatsApp AccessToken غير مضبوط."
            };
        }

        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return new WhatsAppSendResult
            {
                Success = false,
                ErrorMessage = "WhatsApp PhoneNumberId غير مضبوط."
            };
        }

        var url =
            $"https://graph.facebook.com/v23.0/{phoneNumberId}/messages";

        var requestBody = new
        {
            messaging_product = "whatsapp",

            to = phoneNumber,

            type = "text",

            text = new
            {
                body = message
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);

            var responseContent =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "WhatsApp API failed. StatusCode: {StatusCode}",
                    response.StatusCode);

                return new WhatsAppSendResult
                {
                    Success = false,
                    ErrorMessage =
                        $"فشل إرسال الرسالة إلى WhatsApp. HTTP {(int)response.StatusCode}"
                };
            }

            using var document =
                JsonDocument.Parse(responseContent);

            string? externalMessageId = null;

            if (document.RootElement.TryGetProperty(
                    "messages",
                    out var messagesElement))
            {
                if (messagesElement.ValueKind ==
                    JsonValueKind.Array &&
                    messagesElement.GetArrayLength() > 0)
                {
                    var firstMessage =
                        messagesElement[0];

                    if (firstMessage.TryGetProperty(
                            "id",
                            out var idElement))
                    {
                        externalMessageId =
                            idElement.GetString();
                    }
                }
            }

            return new WhatsAppSendResult
            {
                Success = true,
                ExternalMessageId = externalMessageId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while sending WhatsApp message.");

            return new WhatsAppSendResult
            {
                Success = false,
                ErrorMessage =
                    "حدث خطأ أثناء الاتصال بخدمة WhatsApp."
            };
        }
    }
}