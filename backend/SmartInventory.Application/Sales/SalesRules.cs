using SmartInventory.Application.Common;

namespace SmartInventory.Application.Sales;

public static class SalesRules
{
    public static decimal ValidateOrder(SalesOrderRequest request)
    {
        request.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (request.CustomerId <= 0 || request.Items is null || request.Items.Count is < 1 or > 100 ||
            request.Items.Any(item => item is null) || request.Notes?.Length > 2000)
            throw new AppValidationException("A positive customerId, 1 to 100 items and notes up to 2,000 characters are required.");
        var duplicate = request.Items.GroupBy(item => item.ProductId).Any(group => group.Count() > 1);
        if (duplicate) throw new AppValidationException("Each product may appear only once in a sales order.");
        decimal total = 0;
        foreach (var item in request.Items)
        {
            if (item.ProductId <= 0 || item.Quantity <= 0 || item.UnitPrice < 0 || decimal.Round(item.UnitPrice, 2) != item.UnitPrice)
                throw new AppValidationException("Use positive product IDs and quantities, and nonnegative unit prices with at most two decimal places.");
            try { total = checked(total + checked(item.UnitPrice * item.Quantity)); }
            catch (OverflowException) { throw new AppValidationException("The sales order total exceeds the supported range."); }
        }
        if (total > 9999999999999999.99m) throw new AppValidationException("The sales order total exceeds the supported range.");
        return total;
    }

    public static void ValidateFulfillment(SalesFulfillmentRequest request)
    {
        request.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (request.RequestId == Guid.Empty || request.Items is null || request.Items.Count is < 1 or > 100 || request.Reason?.Length > 1000)
            throw new AppValidationException("A nonempty requestId, 1 to 100 fulfillment lines and a reason up to 1,000 characters are required.");
        if (request.Items.Any(item => item is null || item.SalesOrderItemId <= 0 || item.Quantity <= 0) ||
            request.Items.Select(item => item.SalesOrderItemId).Distinct().Count() != request.Items.Count)
            throw new AppValidationException("Each fulfillment line must have a positive order-item ID and quantity, with no duplicate order items.");
    }
}
