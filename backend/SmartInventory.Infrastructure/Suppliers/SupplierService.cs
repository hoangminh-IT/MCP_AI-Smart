using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Suppliers;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Suppliers;

public sealed class SupplierService(AppDbContext database) : ISupplierService
{
    public async Task<PagedResult<SupplierDto>> GetSuppliersAsync(SupplierQuery query, CancellationToken cancellationToken)
    {
        BusinessPartnerRules.ValidateQuery(query.Page, query.PageSize, query.Search);
        var suppliers = database.Suppliers.AsNoTracking().AsQueryable();
        if (query.IsActive.HasValue) suppliers = suppliers.Where(supplier => supplier.IsActive == query.IsActive.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            suppliers = suppliers.Where(supplier => EF.Functions.ILike(supplier.Code, pattern) ||
                EF.Functions.ILike(supplier.Name, pattern) ||
                supplier.ContactPerson != null && EF.Functions.ILike(supplier.ContactPerson, pattern) ||
                supplier.Email != null && EF.Functions.ILike(supplier.Email, pattern) ||
                supplier.Phone != null && EF.Functions.ILike(supplier.Phone, pattern) ||
                supplier.TaxCode != null && EF.Functions.ILike(supplier.TaxCode, pattern));
        }
        var sorted = (query.SortBy?.Trim().ToLowerInvariant() ?? "name") switch
        {
            "name" => query.Descending ? suppliers.OrderByDescending(supplier => supplier.Name) : suppliers.OrderBy(supplier => supplier.Name),
            "code" => query.Descending ? suppliers.OrderByDescending(supplier => supplier.Code) : suppliers.OrderBy(supplier => supplier.Code),
            "contactperson" => query.Descending ? suppliers.OrderByDescending(supplier => supplier.ContactPerson) : suppliers.OrderBy(supplier => supplier.ContactPerson),
            "email" => query.Descending ? suppliers.OrderByDescending(supplier => supplier.Email) : suppliers.OrderBy(supplier => supplier.Email),
            "isactive" => query.Descending ? suppliers.OrderByDescending(supplier => supplier.IsActive) : suppliers.OrderBy(supplier => supplier.IsActive),
            "createdat" => query.Descending ? suppliers.OrderByDescending(supplier => supplier.CreatedAt) : suppliers.OrderBy(supplier => supplier.CreatedAt),
            "updatedat" => query.Descending ? suppliers.OrderByDescending(supplier => supplier.UpdatedAt) : suppliers.OrderBy(supplier => supplier.UpdatedAt),
            _ => throw new AppValidationException("Unsupported supplier sortBy. Use name, code, contactPerson, email, isActive, createdAt or updatedAt.")
        };
        var count = await suppliers.CountAsync(cancellationToken);
        var items = await Project(sorted.ThenBy(supplier => supplier.Id).Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)).ToListAsync(cancellationToken);
        return new(items, count, query.Page, query.PageSize);
    }

    public async Task<SupplierDto> GetSupplierAsync(int id, CancellationToken cancellationToken) =>
        await Project(database.Suppliers.AsNoTracking().Where(supplier => supplier.Id == id))
            .SingleOrDefaultAsync(cancellationToken) ?? throw new AppNotFoundException("Supplier was not found.");

    public async Task<SupplierDto> CreateSupplierAsync(SupplierRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        await EnsureUniqueCodeAsync(request.Code, null, cancellationToken);
        var supplier = new Supplier();
        Apply(supplier, request);
        database.Suppliers.Add(supplier);
        await database.SaveChangesAsync(cancellationToken);
        return await GetSupplierAsync(supplier.Id, cancellationToken);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(int id, SupplierRequest request, CancellationToken cancellationToken)
    {
        NormalizeAndValidate(request);
        var supplier = await database.Suppliers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Supplier was not found.");
        await EnsureUniqueCodeAsync(request.Code, id, cancellationToken);
        Apply(supplier, request);
        await database.SaveChangesAsync(cancellationToken);
        return await GetSupplierAsync(id, cancellationToken);
    }

    public async Task DeactivateSupplierAsync(int id, CancellationToken cancellationToken)
    {
        var supplier = await database.Suppliers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new AppNotFoundException("Supplier was not found.");
        supplier.IsActive = false;
        supplier.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueCodeAsync(string code, int? excludedId, CancellationToken cancellationToken)
    {
        if (await database.Suppliers.AnyAsync(supplier => supplier.Code.ToUpper() == code && supplier.Id != excludedId, cancellationToken))
            throw new AppConflictException("A supplier with this code already exists.");
    }

    private static IQueryable<SupplierDto> Project(IQueryable<Supplier> suppliers) => suppliers.Select(supplier =>
        new SupplierDto(supplier.Id, supplier.Code, supplier.Name, supplier.ContactPerson, supplier.Email,
            supplier.Phone, supplier.Address, supplier.TaxCode, supplier.IsActive, supplier.CreatedAt, supplier.UpdatedAt));

    private static void NormalizeAndValidate(SupplierRequest request)
    {
        request.Code = BusinessPartnerRules.NormalizeCode(request.Code);
        request.Name = BusinessPartnerRules.NormalizeName(request.Name);
        request.ContactPerson = BusinessPartnerRules.NormalizeOptional(request.ContactPerson);
        request.Email = BusinessPartnerRules.NormalizeOptional(request.Email);
        request.Phone = BusinessPartnerRules.NormalizeOptional(request.Phone);
        request.Address = BusinessPartnerRules.NormalizeOptional(request.Address);
        request.TaxCode = BusinessPartnerRules.NormalizeOptional(request.TaxCode);
        BusinessPartnerRules.ValidateRequest(request, request.Email, "Supplier validation failed.");
    }

    private static void Apply(Supplier supplier, SupplierRequest request)
    {
        supplier.Code = request.Code;
        supplier.Name = request.Name;
        supplier.ContactPerson = request.ContactPerson;
        supplier.Email = request.Email;
        supplier.Phone = request.Phone;
        supplier.Address = request.Address;
        supplier.TaxCode = request.TaxCode;
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;
    }
}
