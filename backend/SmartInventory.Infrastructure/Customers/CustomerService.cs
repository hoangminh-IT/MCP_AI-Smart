using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Customers;
using SmartInventory.Application.Entities;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Customers;

public sealed class CustomerService(AppDbContext database) : ICustomerService
{
    public async Task<PagedResult<CustomerDto>> GetCustomersAsync(CustomerQuery query, CancellationToken cancellationToken)
    {
        BusinessPartnerRules.ValidateQuery(query.Page, query.PageSize, query.Search);
        var customers = database.Customers.AsNoTracking().AsQueryable();
        if (query.IsActive.HasValue) customers = customers.Where(customer => customer.IsActive == query.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            customers = customers.Where(customer => EF.Functions.ILike(customer.Code, pattern) || EF.Functions.ILike(customer.Name, pattern) ||
                customer.Email != null && EF.Functions.ILike(customer.Email, pattern) ||
                customer.Phone != null && EF.Functions.ILike(customer.Phone, pattern) ||
                customer.Address != null && EF.Functions.ILike(customer.Address, pattern));
        }
        var sorted = (query.SortBy?.Trim().ToLowerInvariant() ?? "name") switch
        {
            "name" => query.Descending ? customers.OrderByDescending(customer => customer.Name) : customers.OrderBy(customer => customer.Name),
            "code" => query.Descending ? customers.OrderByDescending(customer => customer.Code) : customers.OrderBy(customer => customer.Code),
            "email" => query.Descending ? customers.OrderByDescending(customer => customer.Email) : customers.OrderBy(customer => customer.Email),
            "isactive" => query.Descending ? customers.OrderByDescending(customer => customer.IsActive) : customers.OrderBy(customer => customer.IsActive),
            "createdat" => query.Descending ? customers.OrderByDescending(customer => customer.CreatedAt) : customers.OrderBy(customer => customer.CreatedAt),
            "updatedat" => query.Descending ? customers.OrderByDescending(customer => customer.UpdatedAt) : customers.OrderBy(customer => customer.UpdatedAt),
            _ => throw new AppValidationException("Unsupported customer sortBy. Use name, code, email, isActive, createdAt or updatedAt.")
        };
        var count = await customers.CountAsync(cancellationToken);
        var items = await Project(sorted.ThenBy(customer => customer.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<CustomerDto> GetCustomerAsync(int id, CancellationToken cancellationToken) =>
        await Project(database.Customers.AsNoTracking().Where(customer => customer.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw new AppNotFoundException("Customer was not found.");

    public async Task<CustomerDto> CreateCustomerAsync(CustomerRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        await EnsureUniqueCodeAsync(request.Code, null, cancellationToken);
        var customer = new Customer();
        Apply(customer, request);
        database.Customers.Add(customer);
        await database.SaveChangesAsync(cancellationToken);
        return await GetCustomerAsync(customer.Id, cancellationToken);
    }

    public async Task<CustomerDto> UpdateCustomerAsync(int id, CustomerRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        var customer = await database.Customers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Customer was not found.");
        await EnsureUniqueCodeAsync(request.Code, id, cancellationToken);
        Apply(customer, request);
        await database.SaveChangesAsync(cancellationToken);
        return await GetCustomerAsync(id, cancellationToken);
    }

    public async Task DeactivateCustomerAsync(int id, CancellationToken cancellationToken)
    {
        var customer = await database.Customers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Customer was not found.");
        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueCodeAsync(string code, int? excludedId, CancellationToken cancellationToken)
    {
        if (await database.Customers.AnyAsync(customer => customer.Code.ToUpper() == code && customer.Id != excludedId, cancellationToken))
            throw new AppConflictException("A customer with this code already exists.");
    }

    private static IQueryable<CustomerDto> Project(IQueryable<Customer> customers) => customers.Select(customer =>
        new CustomerDto(customer.Id, customer.Code, customer.Name, customer.Email, customer.Phone, customer.Address,
            customer.IsActive, customer.CreatedAt, customer.UpdatedAt));

    private static void NormalizeAndValidate(CustomerRequest request)
    {
        request.Code = BusinessPartnerRules.NormalizeCode(request.Code);
        request.Name = BusinessPartnerRules.NormalizeName(request.Name);
        request.Email = BusinessPartnerRules.NormalizeOptional(request.Email);
        request.Phone = BusinessPartnerRules.NormalizeOptional(request.Phone);
        request.Address = BusinessPartnerRules.NormalizeOptional(request.Address);
        BusinessPartnerRules.ValidateRequest(request, request.Email, "Customer validation failed.");
    }

    private static void Apply(Customer customer, CustomerRequest request)
    {
        customer.Code = request.Code;
        customer.Name = request.Name;
        customer.Email = request.Email;
        customer.Phone = request.Phone;
        customer.Address = request.Address;
        customer.IsActive = request.IsActive;
        customer.UpdatedAt = DateTime.UtcNow;
    }
}
