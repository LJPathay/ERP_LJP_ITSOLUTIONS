using System;
using System.Threading.Tasks;
using ljp_itsolutions.Models;

namespace ljp_itsolutions.Services
{
    public interface IReceiptService
    {
        /// <summary>
        /// Sends a digital receipt for a specific order.
        /// </summary>
        /// <param name="orderId">The ID of the order.</param>
        /// <param name="targetEmail">Optional override email. If null, uses the customer's email from the order.</param>
        /// <returns>True if the email was queued/sent successfully.</returns>
        Task<bool> SendOrderReceiptAsync(Guid orderId, string? targetEmail = null);

        /// <summary>
        /// </summary>
        /// <param name="shiftId">The ID of the shift to report on.</param>
        /// <returns>True if the email was sent successfully.</returns>
        Task<bool> SendShiftReportAsync(int shiftId);

        /// <summary>
        /// Sends an alert email to the manager when an ingredient hits low stock.
        /// </summary>
        /// <param name="ingredientId">The ID of the ingredient.</param>
        /// <returns>True if the email was sent successfully.</returns>
        Task<bool> SendLowStockAlertAsync(int ingredientId);

        /// <summary>
        /// Sends a comprehensive sales report to the manager for a specific date range.
        /// </summary>
        /// <param name="startDate">Start date of the report.</param>
        /// <param name="endDate">End date of the report.</param>
        /// <returns>True if the email was sent successfully.</returns>
        Task<bool> SendSalesReportAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Sends a welcome email to a newly created staff member with their credentials.
        /// </summary>
        Task<bool> SendWelcomeEmailAsync(User user, string plainPassword);

        /// <summary>
        /// Notifies the marketing team about a promotion's approval or rejection.
        /// </summary>
        Task<bool> SendPromotionStatusAlertAsync(Promotion promotion);
    }
}
