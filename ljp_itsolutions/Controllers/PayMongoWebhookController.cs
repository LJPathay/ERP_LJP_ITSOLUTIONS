using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace ljp_itsolutions.Controllers
{
    [ApiController]
    [Route("api/paymongo")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class PayMongoWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<PayMongoWebhookController> _logger;
        private readonly IConfiguration _configuration;

        public PayMongoWebhookController(ApplicationDbContext db, ILogger<PayMongoWebhookController> logger, IConfiguration configuration)
        {
            _db = db;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            var signatureHeader = Request.Headers["paymongo-signature"].ToString();
            var webhookSecret = _configuration.GetSection("PayMongo:WebhookSecretKey").Value;

            // Read raw body to ensure signature match
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            var requestBodyBytes = ms.ToArray();
            var json = Encoding.UTF8.GetString(requestBodyBytes);

            // Verify Signature
            if (!string.IsNullOrEmpty(signatureHeader) && !string.IsNullOrEmpty(webhookSecret))
            {
                if (!VerifySignature(requestBodyBytes, signatureHeader, webhookSecret))
                {
                    _logger.LogWarning("PayMongo Webhook Signature Verification Failed!");
                    return BadRequest("Invalid Signature");
                }
            }

            try
            {
                var payload = JsonDocument.Parse(json);
                var root = payload.RootElement;
                
                // Extract event type
                var eventType = root.GetProperty("data").GetProperty("attributes").GetProperty("type").GetString();
                
                if (eventType == "payment.paid" || eventType == "checkout_session.payment.paid")
                {
                    var dataObj = root.GetProperty("data").GetProperty("attributes").GetProperty("data");
                    var attributes = dataObj.GetProperty("attributes");
                    
                    string? externalRef = null;

                    // Check for external_reference in root attributes
                    if (attributes.TryGetProperty("external_reference", out var refProperty) && refProperty.ValueKind != JsonValueKind.Null)
                    {
                        externalRef = refProperty.GetString();
                    }
                    // Check for external_reference in metadata
                    else if (attributes.TryGetProperty("metadata", out var metaProp) && metaProp.TryGetProperty("external_reference", out var metaRefProp))
                    {
                        externalRef = metaRefProp.GetString();
                    }

                    }
                    
                    if (!string.IsNullOrEmpty(externalRef) && Guid.TryParse(externalRef, out var orderId))
                    {
                        var order = await _db.Orders
                            .Include(o => o.Payments)
                            .FirstOrDefaultAsync(o => o.OrderID == orderId);

                        if (order != null)
                        {
                            order.PaymentStatus = "Paid"; 
                            
                            // Update or Add Payment record
                            var payment = order.Payments.FirstOrDefault(p => p.PaymentMethod.Contains("Paymongo") || p.PaymentMethod == "E-Wallet");
                            if (payment != null)
                            {
                                payment.PaymentStatus = "Completed";
                                payment.ReferenceNumber = dataObj.GetProperty("id").GetString(); 
                            }
                            else
                            {
                                _db.Payments.Add(new Payment
                                {
                                    OrderID = order.OrderID,
                                    AmountPaid = order.TotalAmount,
                                    PaymentMethod = "Paymongo E-Wallet",
                                    PaymentStatus = "Completed",
                                    PaymentDate = DateTime.Now,
                                    ReferenceNumber = dataObj.GetProperty("id").GetString()
                                });
                            }
                            
                            
                            await _db.SaveChangesAsync();
                            _logger.LogInformation("Order {OrderId} marked as paid via webhook.", orderId);
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PayMongo webhook.");
                return BadRequest();
            }
        }


        private bool VerifySignature(byte[] payloadBytes, string signatureHeader, string secret)
        {
            try
            {
                // PayMongo signature format: t=<timestamp>,te=<test_signature>,li=<live_signature>
                var parts = signatureHeader.Split(',');
                var timestamp = parts.FirstOrDefault(p => p.StartsWith("t="))?.Substring(2);
                var liveSignature = parts.FirstOrDefault(p => p.StartsWith("li="))?.Substring(3);
                var testSignature = parts.FirstOrDefault(p => p.StartsWith("te="))?.Substring(3);

                var signatureToCompare = !string.IsNullOrEmpty(liveSignature) ? liveSignature : testSignature;

                if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signatureToCompare))
                    return false;

                // Concatenate timestamp and dot as bytes, then append raw payload bytes
                var timestampBytes = Encoding.UTF8.GetBytes(timestamp);
                var dotBytes = Encoding.UTF8.GetBytes(".");
                
                var baseBytes = new byte[timestampBytes.Length + dotBytes.Length + payloadBytes.Length];
                Buffer.BlockCopy(timestampBytes, 0, baseBytes, 0, timestampBytes.Length);
                Buffer.BlockCopy(dotBytes, 0, baseBytes, timestampBytes.Length, dotBytes.Length);
                Buffer.BlockCopy(payloadBytes, 0, baseBytes, timestampBytes.Length + dotBytes.Length, payloadBytes.Length);

                var keyBytes = Encoding.UTF8.GetBytes(secret);

                using var hmac = new HMACSHA256(keyBytes);
                var hashBytes = hmac.ComputeHash(baseBytes);
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                return hashString == signatureToCompare;
            }
            catch
            {
                return false;
            }
        }
    }
}
