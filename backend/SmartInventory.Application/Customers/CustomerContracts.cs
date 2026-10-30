using System.ComponentModel.DataAnnotations;
using SmartInventory.Application.Catalog;

namespace SmartInventory.Application.Customers;

public sealed class CustomerQuery
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "name";
    public bool Descending { get; set; }
}

public sealed class CustomerRequest
{
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(254)] public string? Email { get; set; }
    [StringLength(40)] public string? Phone { get; set; }
    [StringLength(1000)] public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record CustomerDto(int Id, string Code, string Name, string? Email,
    string? Phone, string? Address, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQuery query, CancellationToken cancellationToken);
    Task<CustomerDto> GetCustomerAsync(int id, CancellationToken cancellationToken);
    Task<CustomerDto> CreateCustomerAsync(CustomerRequest request, CancellationToken cancellationToken);
    Task<CustomerDto> UpdateCustomerAsync(int id, CustomerRequest request, CancellationToken cancellationToken);
    Task DeactivateCustomerAsync(int id, CancellationToken cancellationToken);
}
