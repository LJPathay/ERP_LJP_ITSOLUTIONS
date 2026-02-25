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
        Task<bool> SendLowStockAlertAsync(int ingredientId);

        /// <summary>
        /// Sends an alert email to the manager when a standalone product hits low stock.
        /// </summary>
        Task<bool> SendProductLowStockAlertAsync(int productId);

        /// <summary>
        /// Sends a comprehensive sales report to the manager for a specific date range.
        /// </summary>
        /// <param name="startDate">Start date of the report.</param>
        /// <param name="endDate">End date of the report.</param>
        /// <returns>True if the email was sent successfully.</returns>
        Task<bool> SendSalesReportAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Sends a welcome email to a newly created staff member with a set-password link.
        /// </summary>
        Task<bool> SendStaffInviteAsync(User user, string inviteLink);

        /// <summary>
        /// Notifies the marketing team about a promotion's approval or rejection.
        /// </summary>
        Task<bool> SendPromotionStatusAlertAsync(Promotion promotion);

        /// <summary>
        /// Sends a loyalty reward promo code directly to the customer's email.
        /// </summary>
        /// <param name="customer">The customer who earned the reward.</param>
        /// <param name="promoCode">The generated discount code.</param>
        /// <param name="discountValue">The discount percentage or amount.</param>
        Task<bool> SendRedemptionCodeEmailAsync(Customer customer, string promoCode, decimal discountValue);

        /// <summary>
        /// Broadcasts a newly approved promotion to all customers who have an email on file.
        /// </summary>
        Task<bool> SendNewPromotionToCustomersAsync(Promotion promotion);
    }
}
