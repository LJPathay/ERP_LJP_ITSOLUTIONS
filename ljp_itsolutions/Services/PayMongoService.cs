using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace ljp_itsolutions.Services
{
    public interface IPayMongoService
    {
        Task<string?> CreateQrPhPaymentAsync(decimal amount, string description, string externalReference);
    }

    public class PayMongoService : IPayMongoService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayMongoService> _logger;

        public PayMongoService(HttpClient httpClient, IConfiguration configuration, ILogger<PayMongoService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> CreateQrPhPaymentAsync(decimal amount, string description, string externalReference)
        {
            var secretKey = _configuration["PayMongo:SecretKey"];
            if (string.IsNullOrEmpty(secretKey))
            {
                _logger.LogError("PayMongo Secret Key is missing in configuration.");
                return null;
            }

            // 1. Create Payment Intent
            var intentData = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount = (int)(amount * 100), // Convert to centavos
                        payment_method_allowed = new[] { "qrph" },
                        payment_method_options = new
                        {
                            card = new { install_type = "default" }
                        },
                        currency = "PHP",
                        description = description,
                        metadata = new { external_reference = externalReference }
                    }
                }
            };

            var intentRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.paymongo.com/v1/payment_intents");
            intentRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(secretKey + ":")));
            intentRequest.Content = new StringContent(JsonSerializer.Serialize(intentData), Encoding.UTF8, "application/json");

            var intentResponse = await _httpClient.SendAsync(intentRequest);
            var intentJson = await intentResponse.Content.ReadAsStringAsync();

            if (!intentResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create PayMongo Payment Intent: {Json}", intentJson);
                return null;
            }

            var intentDoc = JsonDocument.Parse(intentJson);
            var intentId = intentDoc.RootElement.GetProperty("data").GetProperty("id").GetString();
            var clientKey = intentDoc.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("client_key").GetString();

            // 2. Create Payment Method (QR Ph)
            var methodData = new
            {
                data = new
                {
                    attributes = new
                    {
                        type = "qrph"
                    }
                }
            };

            var methodRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.paymongo.com/v1/payment_methods");
            methodRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(secretKey + ":")));
            methodRequest.Content = new StringContent(JsonSerializer.Serialize(methodData), Encoding.UTF8, "application/json");

            var methodResponse = await _httpClient.SendAsync(methodRequest);
            var methodJson = await methodResponse.Content.ReadAsStringAsync();

            if (!methodResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create PayMongo Payment Method: {Json}", methodJson);
                return null;
            }

            var methodDoc = JsonDocument.Parse(methodJson);
            var methodId = methodDoc.RootElement.GetProperty("data").GetProperty("id").GetString();

            // 3. Attach Payment Method to Payment Intent
            var attachData = new
            {
                data = new
                {
                    attributes = new
                    {
                        payment_method = methodId,
                        client_key = clientKey,
                        return_url = "https://ljp-itsolutions.runasp.net/POS/TransactionHistory"
                    }
                }
            };

            var attachRequest = new HttpRequestMessage(HttpMethod.Post, $"https://api.paymongo.com/v1/payment_intents/{intentId}/attach");
            attachRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(secretKey + ":")));
            attachRequest.Content = new StringContent(JsonSerializer.Serialize(attachData), Encoding.UTF8, "application/json");

            var attachResponse = await _httpClient.SendAsync(attachRequest);
            var attachJson = await attachResponse.Content.ReadAsStringAsync();

            if (!attachResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to attach PayMongo Payment Method: {Json}", attachJson);
                return null;
            }

            var attachDoc = JsonDocument.Parse(attachJson);
            // The QR code image data is usually in next_action.show_qr_code
            if (attachDoc.RootElement.GetProperty("data").GetProperty("attributes").TryGetProperty("next_action", out var nextAction))
            {
                if (nextAction.TryGetProperty("code", out var qrCode))
                {
                    if (qrCode.TryGetProperty("image_url", out var imageUrl))
                        return imageUrl.GetString();
                    
                    if (qrCode.TryGetProperty("data", out var data))
                        return "data:image/png;base64," + data.GetString();
                }

                // Fallback for other potential types (e.g. redirect) or if API changes
                if (nextAction.TryGetProperty("show_qr_code", out var showQr))
                {
                     if (showQr.TryGetProperty("image_url", out var imageUrl))
                        return imageUrl.GetString();
                }
            }

            return null;
        }
    }
}
