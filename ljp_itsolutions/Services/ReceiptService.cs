using ljp_itsolutions.Data;
using ljp_itsolutions.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ljp_itsolutions.Services
{
    public class ReceiptService : IReceiptService
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ReceiptService> _logger;

        public ReceiptService(ApplicationDbContext db, IEmailSender emailSender, ILogger<ReceiptService> logger)
        {
            _db = db;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<bool> SendOrderReceiptAsync(Guid orderId, string? targetEmail = null)
        {
            if (!await IsFeatureEnabledAsync("EmailNotifications")) return false;

            var order = await _db.Orders
                .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                .Include(o => o.Cashier)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.OrderID == orderId);

            if (order == null)
            {
                _logger.LogWarning("SendOrderReceiptAsync: Order {OrderId} not found.", orderId);
                return false;
            }

            string? email = targetEmail ?? order.Customer?.Email;
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogInformation("SendOrderReceiptAsync: No email found for Order {OrderId}.", orderId);
                return false;
            }

            string htmlBody = $@"
                <div style='background-color: #f4f7f6; padding: 40px 0; font-family: ""Helvetica Neue"", Helvetica, Arial, sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.08); border: 1px solid #e1e8e5;'>
                        <div style='background: linear-gradient(135deg, #1a2a6c, #b21f1f, #fdbb2d); padding: 30px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 700; letter-spacing: 1px;'>LJP IT SOLUTIONS</h1>
                            <p style='margin: 5px 0 0; opacity: 0.9; font-size: 14px; text-transform: uppercase; letter-spacing: 2px;'>Coffee Experience</p>
                        </div>
                        
                        <div style='padding: 30px;'>
                            <h2 style='color: #2c3e50; font-size: 20px; text-align: center; margin-bottom: 25px;'>Thank you for your patronage!</h2>
                            <p style='color: #7f8c8d; font-size: 14px; line-height: 1.6;'>Hello <strong>{order.Customer?.FullName ?? "Valued Customer"}</strong>,</p>
                            <p style='color: #7f8c8d; font-size: 14px; line-height: 1.6;'>We appreciate you choosing LJP Coffee. Your order has been processed successfully. Below are your transaction details.</p>
                            
                            <div style='background-color: #f9fbfb; border-radius: 8px; padding: 20px; margin: 25px 0; border: 1px solid #edf2f2;'>
                                <table style='width: 100%; font-size: 13px; color: #34495e;'>
                                    <tr>
                                        <td style='padding-bottom: 8px;'><strong>Order Number:</strong></td>
                                        <td style='text-align: right; padding-bottom: 8px;'>#ORD-{order.OrderID.ToString().Substring(0, 8).ToUpper()}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding-bottom: 8px;'><strong>Transaction Date:</strong></td>
                                        <td style='text-align: right; padding-bottom: 8px;'>{order.OrderDate.ToString("MMMM dd, yyyy · hh:mm tt")}</td>
                                    </tr>
                                    <tr>
                                        <td><strong>Processed By:</strong></td>
                                        <td style='text-align: right;'>{order.Cashier?.FullName ?? "System"}</td>
                                    </tr>
                                </table>
                            </div>

                            <table style='width: 100%; border-collapse: collapse; margin-bottom: 20px;'>
                                <thead>
                                    <tr style='border-bottom: 2px solid #edf2f2;'>
                                        <th style='text-align: left; padding: 12px 0; font-size: 14px; color: #2c3e50;'>Item Description</th>
                                        <th style='text-align: center; padding: 12px 0; font-size: 14px; color: #2c3e50;'>Qty</th>
                                        <th style='text-align: right; padding: 12px 0; font-size: 14px; color: #2c3e50;'>Amount</th>
                                    </tr>
                                </thead>
                                <tbody>";

            foreach (var item in order.OrderDetails)
            {
                htmlBody += $@"
                                    <tr style='border-bottom: 1px solid #f9fbfb;'>
                                        <td style='padding: 12px 0; font-size: 14px; color: #576574;'>{item.Product?.ProductName}</td>
                                        <td style='padding: 12px 0; text-align: center; font-size: 14px; color: #576574;'>{item.Quantity}</td>
                                        <td style='padding: 12px 0; text-align: right; font-size: 14px; color: #2c3e50; font-weight: 600;'>₱{item.Subtotal:N2}</td>
                                    </tr>";
            }

            htmlBody += $@"
                                </tbody>
                            </table>

                            <div style='border-top: 2px solid #edf2f2; padding-top: 15px;'>
                                <table style='width: 100%;'>
                                    <tr>
                                        <td style='font-size: 14px; color: #7f8c8d;'>Subtotal</td>
                                        <td style='text-align: right; font-size: 14px; color: #2c3e50;'>₱{order.TotalAmount:N2}</td>
                                    </tr>
                                    {(order.DiscountAmount > 0 ? $@"
                                    <tr>
                                        <td style='font-size: 14px; color: #e74c3c;'>Discount Applied</td>
                                        <td style='text-align: right; font-size: 14px; color: #e74c3c;'>-₱{order.DiscountAmount:N2}</td>
                                    </tr>" : "")}
                                    <tr>
                                        <td style='font-size: 18px; font-weight: 700; color: #2c3e50; padding-top: 10px;'>Total Amount</td>
                                        <td style='text-align: right; font-size: 20px; font-weight: 700; color: #1a2a6c; padding-top: 10px;'>₱{order.FinalAmount:N2}</td>
                                    </tr>
                                </table>
                            </div>

                            <div style='margin-top: 40px; padding-top: 20px; border-top: 1px solid #edf2f2; text-align: center;'>
                                <p style='color: #7f8c8d; font-size: 12px; line-height: 1.5;'>
                                    If you have any questions about your order, please contact our support team.<br>
                                    © {DateTime.Now.Year} LJP IT Solutions. All rights reserved.
                                </p>
                            </div>
                        </div>
                    </div>
                </div>";

            try
            {
                await _emailSender.SendEmailAsync(email, $"Electronic Receipt - Order #{order.OrderID.ToString().Substring(0, 8).ToUpper()}", htmlBody);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send receipt email to {Email}", email);
                return false;
            }
        }

        public async Task<bool> SendShiftReportAsync(int shiftId)
        {
            if (!await IsFeatureEnabledAsync("EmailNotifications") || !await IsFeatureEnabledAsync("DailyReports")) return false;

            var shift = await _db.CashShifts
                .Include(s => s.Cashier)
                .FirstOrDefaultAsync(s => s.CashShiftID == shiftId);

            if (shift == null) return false;

            // Gather sales data for this shift
            var shiftOrders = await _db.Orders
                .Include(o => o.OrderDetails)
                .Where(o => o.OrderDate >= shift.StartTime && o.OrderDate <= (shift.EndTime ?? DateTime.Now))
                .ToListAsync();

            var totalSales = shiftOrders.Sum(o => o.FinalAmount);
            var cashSales = shiftOrders.Where(o => o.PaymentMethod == "Cash").Sum(o => o.FinalAmount);
            var digitalSales = totalSales - cashSales;
            var ordersCount = shiftOrders.Count;

            string htmlBody = $@"
                <div style='background-color: #f8fafc; padding: 40px 0; font-family: ""Inter"", -apple-system, sans-serif;'>
                    <div style='max-width: 650px; margin: 0 auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); border: 1px solid #e2e8f0;'>
                        <div style='background-color: #0f172a; padding: 30px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 22px; font-weight: 700; letter-spacing: 0.5px;'>SHIFT CLOSURE REPORT</h1>
                            <p style='margin: 5px 0 0; opacity: 0.7; font-size: 13px; text-transform: uppercase;'>Z-Report · {DateTime.Now:MMMM dd, yyyy}</p>
                        </div>
                        
                        <div style='padding: 30px;'>
                            <div style='display: flex; justify-content: space-between; margin-bottom: 30px; border-bottom: 1px solid #f1f5f9; padding-bottom: 20px;'>
                                <div style='flex: 1;'>
                                    <p style='margin: 0; color: #64748b; font-size: 12px; font-weight: 600; text-transform: uppercase;'>Cashier</p>
                                    <p style='margin: 4px 0 0; color: #1e293b; font-size: 15px; font-weight: 700;'>{shift.Cashier?.FullName ?? "Unknown"}</p>
                                </div>
                                <div style='flex: 1; text-align: right;'>
                                    <p style='margin: 0; color: #64748b; font-size: 12px; font-weight: 600; text-transform: uppercase;'>Shift ID</p>
                                    <p style='margin: 4px 0 0; color: #1e293b; font-size: 15px; font-weight: 700;'>#SH-{shift.CashShiftID:D4}</p>
                                </div>
                            </div>

                            <div style='margin-bottom: 35px;'>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr>
                                        <td style='padding: 12px; background-color: #f8fafc; border-radius: 8px 0 0 8px; border: 1px solid #e2e8f0; border-right: none;'>
                                            <p style='margin: 0; color: #64748b; font-size: 11px; font-weight: 700; text-transform: uppercase;'>Shift Start</p>
                                            <p style='margin: 4px 0 0; color: #1e293b; font-size: 14px; font-weight: 600;'>{shift.StartTime:hh:mm tt}</p>
                                        </td>
                                        <td style='padding: 12px; background-color: #f8fafc; border-top: 1px solid #e2e8f0; border-bottom: 1px solid #e2e8f0;'>
                                            <p style='margin: 0; color: #64748b; font-size: 11px; font-weight: 700; text-transform: uppercase;'>Shift End</p>
                                            <p style='margin: 4px 0 0; color: #1e293b; font-size: 14px; font-weight: 600;'>{shift.EndTime?.ToString("hh:mm tt") ?? "--:--"}</p>
                                        </td>
                                        <td style='padding: 12px; background-color: #f8fafc; border-radius: 0 8px 8px 0; border: 1px solid #e2e8f0; border-left: none; text-align: right;'>
                                            <p style='margin: 0; color: #64748b; font-size: 11px; font-weight: 700; text-transform: uppercase;'>Duration</p>
                                            <p style='margin: 4px 0 0; color: #1e293b; font-size: 14px; font-weight: 600;'>{(shift.EndTime - shift.StartTime)?.ToString(@"h\h\ m\m") ?? "N/A"}</p>
                                        </td>
                                    </tr>
                                </table>
                            </div>

                            <h3 style='color: #334155; font-size: 16px; font-weight: 700; margin-bottom: 15px;'>Financial Summary</h3>
                            <div style='background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; margin-bottom: 30px;'>
                                <table style='width: 100%; border-collapse: collapse;'>
                                    <tr>
                                        <td style='padding: 15px; border-bottom: 1px solid #f1f5f9; color: #64748b; font-size: 14px;'>Opening Drawer (Float)</td>
                                        <td style='padding: 15px; border-bottom: 1px solid #f1f5f9; text-align: right; font-weight: 600; color: #1e293b;'>₱{shift.StartingCash:N2}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 15px; border-bottom: 1px solid #f1f5f9; color: #64748b; font-size: 14px;'>Cash Sales</td>
                                        <td style='padding: 15px; border-bottom: 1px solid #f1f5f9; text-align: right; font-weight: 600; color: #10b981;'>+₱{cashSales:N2}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 15px; border-bottom: 1px solid #f1f5f9; color: #64748b; font-size: 14px;'>Expected Cash in Drawer</td>
                                        <td style='padding: 15px; border-bottom: 1px solid #f1f5f9; text-align: right; font-weight: 600; color: #1e293b;'>₱{shift.ExpectedEndingCash:N2}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 15px; border-bottom: 2px solid #f1f5f9; color: #64748b; font-size: 14px;'>Actual Cash Counted</td>
                                        <td style='padding: 15px; border-bottom: 2px solid #f1f5f9; text-align: right; font-weight: 700; color: #1e293b;'>₱{shift.ActualEndingCash:N2}</td>
                                    </tr>
                                    <tr style='background-color: {(shift.Difference >= 0 ? "#f0fdf4" : "#fef2f2")};'>
                                        <td style='padding: 15px; font-weight: 700; color: {(shift.Difference >= 0 ? "#15803d" : "#b91c1c")}; font-size: 14px;'>Cash Difference (Overage/Shortage)</td>
                                        <td style='padding: 15px; text-align: right; font-weight: 800; color: {(shift.Difference >= 0 ? "#15803d" : "#b91c1c")}; font-size: 16px;'>₱{shift.Difference:N2}</td>
                                    </tr>
                                </table>
                            </div>

                            <div style='background-color: #f8fafc; border-radius: 12px; padding: 20px; border: 1px solid #e2e8f0;'>
                                <div style='display: flex; justify-content: space-between;'>
                                    <div>
                                        <p style='margin: 0; color: #64748b; font-size: 12px; font-weight: 600;'>Total Orders</p>
                                        <p style='margin: 4px 0 0; color: #1e293b; font-size: 18px; font-weight: 800;'>{ordersCount}</p>
                                    </div>
                                    <div style='text-align: center;'>
                                        <p style='margin: 0; color: #64748b; font-size: 12px; font-weight: 600;'>Digital Sales</p>
                                        <p style='margin: 4px 0 0; color: #3b82f6; font-size: 18px; font-weight: 800;'>₱{digitalSales:N2}</p>
                                    </div>
                                    <div style='text-align: right;'>
                                        <p style='margin: 0; color: #64748b; font-size: 12px; font-weight: 600;'>Gross Revenue</p>
                                        <p style='margin: 4px 0 0; color: #1e293b; font-size: 18px; font-weight: 800;'>₱{totalSales:N2}</p>
                                    </div>
                                </div>
                            </div>

                            <div style='margin-top: 40px; text-align: center;'>
                                <p style='color: #94a3b8; font-size: 12px;'>
                                    This is an automated system report from LJP IT Solutions ERP.<br>
                                    Report Generated at {DateTime.Now:hh:mm tt}
                                </p>
                            </div>
                        </div>
                    </div>
                </div>";

            try
            {
                var recipientEmails = await GetManagementEmailsAsync();
                
                // Generate CSV of orders for this shift
                var csv = new System.Text.StringBuilder();
                csv.Append('\uFEFF');
                csv.AppendLine("Order ID,Time,Payment,Total");
                foreach (var o in shiftOrders)
                {
                    csv.AppendLine($"\"{o.OrderID.ToString().Substring(0, 8)}\",\"{o.OrderDate:HH:mm}\",\"{o.PaymentMethod}\",\"{o.FinalAmount:N2}\"");
                }
                var csvBytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

                var attachments = new Dictionary<string, byte[]>
                {
                    { $"Shift_Summary_{shift.CashShiftID}.csv", csvBytes }
                };

                int successCount = 0;
                foreach (var email in recipientEmails)
                {
                    try {
                        await _emailSender.SendEmailAsync(email, $"Z-REPORT: Shift #{shift.CashShiftID:D4} Closed by {shift.Cashier?.FullName}", htmlBody, attachments);
                        successCount++;
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to send shift report to {Email}", email);
                        Console.WriteLine($"[Individual SMTP Error]: {email} - {ex.Message}");
                    }
                }
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send shift report for Shift ID {ShiftId}", shiftId);
                return false;
            }
        }

        public async Task<bool> SendLowStockAlertAsync(int ingredientId)
        {
            if (!await IsFeatureEnabledAsync("EmailNotifications") || !await IsFeatureEnabledAsync("LowStockAlerts")) return false;

            var ingredient = await _db.Ingredients.FindAsync(ingredientId);
            if (ingredient == null) return false;

            string htmlBody = $@"
                <div style='background-color: #fff1f2; padding: 40px 0; font-family: ""Inter"", -apple-system, sans-serif;'>
                    <div style='max-width: 500px; margin: 0 auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(225, 29, 72, 0.1); border: 1px solid #fecdd3;'>
                        <div style='background-color: #be123c; padding: 25px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 20px; font-weight: 800; letter-spacing: 1px;'>URGENT: LOW STOCK ALERT</h1>
                        </div>
                        
                        <div style='padding: 30px; text-align: center;'>
                            <div style='width: 64px; height: 64px; background-color: #fff1f2; border-radius: 50%; display: inline-flex; align-items: center; justify-content: center; margin-bottom: 20px; border: 2px solid #fb7185;'>
                                <span style='font-size: 32px;'>⚠️</span>
                            </div>
                            
                            <h2 style='color: #1e293b; font-size: 18px; font-weight: 700; margin-bottom: 10px;'>{ingredient.Name} is running out!</h2>
                            <p style='color: #64748b; font-size: 14px; line-height: 1.6; margin-bottom: 25px;'>
                                This is an automated alert to notify you that your inventory for <strong>{ingredient.Name}</strong> is below the critical threshold.
                            </p>
                            
                            <div style='background-color: #f8fafc; border-radius: 12px; padding: 20px; border: 1px solid #e2e8f0; margin-bottom: 30px;'>
                                <table style='width: 100%;'>
                                    <tr>
                                        <td style='text-align: left; color: #64748b; font-size: 12px; font-weight: 600; text-transform: uppercase;'>Current Stock</td>
                                        <td style='text-align: right; color: #be123c; font-size: 18px; font-weight: 800;'>{ingredient.StockQuantity:0.##} {ingredient.Unit}</td>
                                    </tr>
                                    <tr>
                                        <td style='text-align: left; color: #64748b; font-size: 12px; font-weight: 600; text-transform: uppercase; padding-top: 10px;'>Threshold</td>
                                        <td style='text-align: right; color: #1e293b; font-size: 14px; font-weight: 600; padding-top: 10px;'>{ingredient.LowStockThreshold:0.##} {ingredient.Unit}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <p style='color: #475569; font-size: 13px; font-style: italic;'>
                                Action recommended: Please reorder this item soon to avoid service interruptions.
                            </p>
                            
                            <div style='margin-top: 40px; border-top: 1px solid #f1f5f9; padding-top: 20px;'>
                                <p style='color: #94a3b8; font-size: 11px;'>
                                    LJP Coffee ERP · Automated Inventory Monitoring System
                                </p>
                            </div>
                        </div>
                    </div>
                </div>";

            try
            {
                var recipientEmails = await GetManagementEmailsAsync();

                foreach (var email in recipientEmails)
                {
                    await _emailSender.SendEmailAsync(email, $"🚨 INVENTORY ALERT: {ingredient.Name} is Low!", htmlBody);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send low stock alert for Ingredient ID {IngredientId}", ingredientId);
                return false;
            }
        }

        public async Task<bool> SendSalesReportAsync(DateTime startDate, DateTime endDate)
        {
            if (!await IsFeatureEnabledAsync("EmailNotifications")) return false;

            var orders = await _db.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Product)
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Paid (Digital)" || o.PaymentStatus == "Completed"))
                .ToListAsync();

            var totalRevenue = orders.Sum(o => o.FinalAmount);
            var totalOrders = orders.Count;
            var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
            
            var topProducts = orders.SelectMany(o => o.OrderDetails)
                .GroupBy(d => d.Product.ProductName)
                .Select(g => new { Name = g.Key, Qty = g.Sum(d => d.Quantity), Total = g.Sum(d => d.Subtotal) })
                .OrderByDescending(x => x.Qty)
                .Take(5)
                .ToList();

            string htmlBody = $@"
                <div style='background-color: #f1f5f9; padding: 40px 0; font-family: ""Inter"", sans-serif;'>
                    <div style='max-width: 700px; margin: 0 auto; background-color: #ffffff; border-radius: 20px; overflow: hidden; box-shadow: 0 15px 35px rgba(0,0,0,0.05); border: 1px solid #e2e8f0;'>
                        <div style='background: linear-gradient(135deg, #1e293b, #334155); padding: 40px 30px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 26px; font-weight: 800; letter-spacing: 1px;'>EXECUTIVE SALES SUMMARY</h1>
                            <p style='margin: 8px 0 0; opacity: 0.8; font-size: 14px;'>{startDate:MMM dd, yyyy} — {endDate:MMM dd, yyyy}</p>
                        </div>
                        
                        <div style='padding: 30px;'>
                            <div style='display: grid; grid-template-columns: repeat(3, 1fr); gap: 15px; margin-bottom: 40px;'>
                                <div style='background-color: #f8fafc; padding: 15px; border-radius: 12px; border: 1px solid #f1f5f9; text-align: center;'>
                                    <p style='margin: 0; color: #64748b; font-size: 11px; font-weight: 700; text-transform: uppercase;'>Total Revenue</p>
                                    <p style='margin: 5px 0 0; color: #0f172a; font-size: 20px; font-weight: 800;'>₱{totalRevenue:N2}</p>
                                </div>
                                <div style='background-color: #f8fafc; padding: 15px; border-radius: 12px; border: 1px solid #f1f5f9; text-align: center;'>
                                    <p style='margin: 0; color: #64748b; font-size: 11px; font-weight: 700; text-transform: uppercase;'>Total Orders</p>
                                    <p style='margin: 5px 0 0; color: #0f172a; font-size: 20px; font-weight: 800;'>{totalOrders}</p>
                                </div>
                                <div style='background-color: #f8fafc; padding: 15px; border-radius: 12px; border: 1px solid #f1f5f9; text-align: center;'>
                                    <p style='margin: 0; color: #64748b; font-size: 11px; font-weight: 700; text-transform: uppercase;'>Avg Ticket</p>
                                    <p style='margin: 5px 0 0; color: #0f172a; font-size: 20px; font-weight: 800;'>₱{avgOrderValue:N2}</p>
                                </div>
                            </div>

                            <h3 style='color: #334155; font-size: 16px; font-weight: 700; border-bottom: 2px solid #f1f5f9; padding-bottom: 10px; margin-bottom: 15px;'>Top Performing Products</h3>
                            <table style='width: 100%; border-collapse: collapse; margin-bottom: 40px;'>
                                <thead>
                                    <tr style='text-align: left; color: #94a3b8; font-size: 12px; font-weight: 700; text-transform: uppercase;'>
                                        <th style='padding: 10px 0;'>Product</th>
                                        <th style='padding: 10px 0; text-align: center;'>Qty Sold</th>
                                        <th style='padding: 10px 0; text-align: right;'>Total Revenue</th>
                                    </tr>
                                </thead>
                                <tbody>";

            foreach (var p in topProducts)
            {
                htmlBody += $@"
                                    <tr style='border-bottom: 1px solid #f8fafc;'>
                                        <td style='padding: 12px 0; font-size: 14px; color: #1e293b; font-weight: 600;'>{p.Name}</td>
                                        <td style='padding: 12px 0; text-align: center; font-size: 14px; color: #64748b;'>{p.Qty:N0}</td>
                                        <td style='padding: 12px 0; text-align: right; font-size: 14px; color: #0f172a; font-weight: 700;'>₱{p.Total:N2}</td>
                                    </tr>";
            }

            htmlBody += $@"
                                </tbody>
                            </table>

                            <div style='background-color: #fffbeb; border: 1px solid #fef3c7; border-radius: 12px; padding: 20px;'>
                                <div style='display: flex; align-items: flex-start; gap: 12px;'>
                                    <div style='font-size: 20px;'>💡</div>
                                    <div>
                                        <p style='margin: 0; color: #92400e; font-size: 14px; font-weight: 700;'>Business Insight</p>
                                        <p style='margin: 4px 0 0; color: #b45309; font-size: 13px; line-height: 1.5;'>
                                            Your best performing item is <strong>{topProducts.FirstOrDefault()?.Name ?? "N/A"}</strong>. 
                                            Consider running a promotion on this item to further drive traffic.
                                        </p>
                                    </div>
                                </div>
                            </div>

                            <div style='margin-top: 40px; text-align: center; border-top: 1px solid #f1f5f9; padding-top: 25px;'>
                                <p style='color: #94a3b8; font-size: 12px;'>
                                    This report was generated on demand via LJP IT Solutions ERP Admin Console.<br>
                                    © {DateTime.Now.Year} LJP IT Solutions. Confidential.
                                </p>
                            </div>
                        </div>
                    </div>
                </div>";

            try
            {
                var recipientEmails = await GetManagementEmailsAsync();
                
                // Generate CSV Attachment
                var csvContent = new System.Text.StringBuilder();
                csvContent.Append('\uFEFF'); // BOM for Excel
                csvContent.AppendLine("Product,Units Sold,Revenue");
                foreach (var p in topProducts)
                {
                    csvContent.AppendLine($"\"{p.Name}\",{p.Qty},\"{p.Total:N2}\"");
                }
                var csvBytes = System.Text.Encoding.UTF8.GetBytes(csvContent.ToString());
                
                var attachments = new Dictionary<string, byte[]>
                {
                    { $"Sales_Report_{startDate:yyyyMMdd}.csv", csvBytes }
                };

                int successCount = 0;
                foreach (var email in recipientEmails)
                {
                    try {
                        await _emailSender.SendEmailAsync(email, $"REPORT: Sales Summary ({startDate:MMM dd} - {endDate:MMM dd})", htmlBody, attachments);
                        successCount++;
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to send sales report to {Email}", email);
                        Console.WriteLine($"[Individual SMTP Error]: {email} - {ex.Message}");
                    }
                }
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send sales report for range {Start} - {End}", startDate, endDate);
                Console.WriteLine($"[SMTP ERROR]: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"[Inner]: {ex.InnerException.Message}");
                return false;
            }
        }

        public async Task<bool> SendWelcomeEmailAsync(User user, string plainPassword)
        {
            if (!await IsFeatureEnabledAsync("EmailNotifications")) return false;

            string htmlBody = $@"
                <div style='background-color: #f8fafc; padding: 40px 0; font-family: ""Inter"", sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); border: 1px solid #e2e8f0;'>
                        <div style='background: linear-gradient(135deg, #1e293b, #334155); padding: 40px 30px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 24px; font-weight: 800;'>WELCOME TO THE TEAM</h1>
                            <p style='margin: 8px 0 0; opacity: 0.8;'>LJP IT Solutions · ERP Staff Account</p>
                        </div>
                        
                        <div style='padding: 30px;'>
                            <p style='color: #1e293b; font-size: 16px;'>Hello <strong>{user.FullName}</strong>,</p>
                            <p style='color: #64748b; font-size: 14px; line-height: 1.6;'>
                                Your staff account has been created for the <strong>LJP Coffee ERP</strong>. You now have access as a <strong>{user.Role}</strong>.
                            </p>
                            
                            <div style='background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 25px; margin: 30px 0;'>
                                <p style='margin: 0 0 15px; color: #64748b; font-size: 12px; font-weight: 700; text-transform: uppercase;'>Your Temporary Credentials</p>
                                <table style='width: 100%; font-size: 14px;'>
                                    <tr>
                                        <td style='color: #94a3b8; padding-bottom: 10px;'>Username:</td>
                                        <td style='color: #1e293b; font-weight: 700; padding-bottom: 10px;'>{user.Username}</td>
                                    </tr>
                                    <tr>
                                        <td style='color: #94a3b8;'>Password:</td>
                                        <td style='color: #1e293b; font-weight: 700;'>{plainPassword}</td>
                                    </tr>
                                </table>
                            </div>

                            <p style='color: #64748b; font-size: 13px; line-height: 1.6;'>
                                <strong>Important:</strong> Please log in and change your password immediately in your Profile settings.
                            </p>

                            <div style='margin-top: 40px; text-align: center;'>
                                <a href='#' style='background-color: #3b82f6; color: #ffffff; padding: 12px 30px; border-radius: 8px; text-decoration: none; font-weight: 700; font-size: 14px;'>Launch ERP Console</a>
                            </div>
                        </div>
                    </div>
                </div>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email ?? "", "Welcome to LJP IT Solutions ERP", htmlBody);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send welcome email to {Email}", user.Email);
                return false;
            }
        }

        public async Task<bool> SendPromotionStatusAlertAsync(Promotion promotion)
        {
            if (!await IsFeatureEnabledAsync("EmailNotifications")) return false;

            bool isApproved = promotion.ApprovalStatus == "Approved";
            string color = isApproved ? "#10b981" : "#ef4444";
            
            string htmlBody = $@"
                <div style='background-color: #f8fafc; padding: 40px 0; font-family: ""Inter"", sans-serif;'>
                    <div style='max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); border: 1px solid #e2e8f0;'>
                        <div style='background-color: {color}; padding: 30px; text-align: center; color: #ffffff;'>
                            <h1 style='margin: 0; font-size: 20px; font-weight: 800;'>CAMPAIGN STATUS UPDATED</h1>
                            <p style='margin: 5px 0 0; opacity: 0.8;'>Promotion Authorization</p>
                        </div>
                        
                        <div style='padding: 30px;'>
                            <h2 style='color: #1e293b; font-size: 18px; font-weight: 700; margin-bottom: 10px;'>{promotion.PromotionName}</h2>
                            <p style='color: #64748b; font-size: 14px;'>Status: <strong style='color: {color};'>{promotion.ApprovalStatus}</strong></p>
                            
                            <div style='background-color: #f8fafc; border-radius: 12px; padding: 20px; border: 1px solid #e2e8f0; margin: 25px 0;'>
                                <p style='margin: 0; color: #64748b; font-size: 13px;'>
                                    {(isApproved ? "The campaign has been approved and is now ready for launch." : $"The campaign was rejected. Reason: {promotion.RejectionReason ?? "No reason provided."}")}
                                </p>
                            </div>
                            
                            <p style='color: #94a3b8; font-size: 12px; text-align: center;'>LJP Coffee Marketing Module</p>
                        </div>
                    </div>
                </div>";

            try
            {
                var staffEmails = await _db.Users
                    .Where(u => (u.Role == UserRoles.MarketingStaff || u.Role == UserRoles.Admin) && !string.IsNullOrEmpty(u.Email) && u.IsActive)
                    .Select(u => u.Email!)
                    .ToListAsync();

                foreach (var email in staffEmails)
                {
                    await _emailSender.SendEmailAsync(email, $"Promotion Update: {promotion.PromotionName} ({promotion.ApprovalStatus})", htmlBody);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send promotion status alert for {Promo}", promotion.PromotionName);
                return false;
            }
        }

        private async Task<bool> IsFeatureEnabledAsync(string key)
        {
            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null) return true;
            return setting.SettingValue?.ToLower() == "true";
        }

        private async Task<List<string>> GetManagementEmailsAsync()
        {
            var systemEmail = await _db.SystemSettings
                .Where(s => s.SettingKey == "ManagerEmail")
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync() ?? "itsolutionslj@gmail.com";

            var roleEmails = await _db.Users
                .Where(u => (u.Role == UserRoles.SuperAdmin || u.Role == UserRoles.Admin || u.Role == UserRoles.Manager) 
                            && !string.IsNullOrEmpty(u.Email) && u.IsActive)
                .Select(u => u.Email!)
                .ToListAsync();

            return roleEmails.Union(new[] { systemEmail }).Distinct().ToList();
        }
    }
}
