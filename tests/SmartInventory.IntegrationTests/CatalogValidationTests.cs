using System.ComponentModel.DataAnnotations;
using System.Globalization;
using SmartInventory.Application.Catalog;
using Xunit;

namespace SmartInventory.IntegrationTests;

public sealed class CatalogValidationTests
{
    [Theory]
    [InlineData("vi-VN", "12.99", true)]
    [InlineData("en-US", "12.99", true)]
    [InlineData("vi-VN", "-1", false)]
    [InlineData("en-US", "-1", false)]
    [InlineData("vi-VN", "9999999999999999.99", true)]
    [InlineData("vi-VN", "10000000000000000", false)]
    public void Monetary_validation_handles_locale_and_numeric_boundary(string locale, string price, bool expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(locale);
            var request = new ProductRequest
            {
                CategoryId = 1, Sku = "VALIDATION", Name = "Validation",
                Price = decimal.Parse(price, CultureInfo.InvariantCulture), CostPrice = 0, MinStock = 0
            };
            Assert.Equal(expected, Validator.TryValidateObject(request, new ValidationContext(request), [], true));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
