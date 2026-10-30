using ApexPost.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;
public sealed class BranchForm { public Guid Id { get; set; } public long Version { get; set; } [Required] public string Code { get; set; } = ""; [Required] public string Name { get; set; } = ""; [Required] public string AddressLine1 { get; set; } = ""; public string? AddressLine2 { get; set; } [Required] public string City { get; set; } = ""; [Required] public string PostalCode { get; set; } = ""; [Required, StringLength(2, MinimumLength = 2)] public string CountryCode { get; set; } = ""; [Range(1, 100)] public int CounterCount { get; set; } = 1; }
public sealed class StaffForm { public Guid Id { get; set; } public long Version { get; set; } [Required] public string EmployeeNumber { get; set; } = ""; [Required] public string Name { get; set; } = ""; [Required] public Guid BranchId { get; set; } public IReadOnlyList<BranchOption> Branches { get; set; } = []; [Required] public string Role { get; set; } = nameof(ApexPost.Domain.Staff.StaffRole.CounterClerk); }
public sealed class ServiceForm { public Guid Id { get; set; } public long Version { get; set; } [Required] public string Code { get; set; } = ""; [Required] public string Name { get; set; } = ""; [Required] public string Description { get; set; } = ""; }
public class ShipmentForm : IBranchSelectionForm
{
    [Required] public Guid BranchId { get; set; }
    public IReadOnlyList<BranchOption> Branches { get; set; } = [];
    public IReadOnlyList<ApexPost.Application.Catalogue.ServiceResponse> Services { get; set; } = [];
    public IReadOnlyList<string> ZoneCodes { get; set; } = [];
    public decimal CurrentServicePrice { get; set; }
    [Required] public string Number { get; set; } = "";
    [Required] public Guid ServiceId { get; set; }
    [Required] public string ZoneCode { get; set; } = "Z01";
    public ApexPost.Domain.Shipping.ShippingServiceLevel ServiceLevel { get; set; } = ApexPost.Domain.Shipping.ShippingServiceLevel.Standard;
    [Required] public string SenderName { get; set; } = "";
    [Required] public string SenderLine1 { get; set; } = "";
    [Required] public string SenderCity { get; set; } = "";
    [Required] public string SenderPostalCode { get; set; } = "";
    [Required] public string SenderCountryCode { get; set; } = "IN";
    [Required] public string RecipientName { get; set; } = "";
    [Required] public string RecipientLine1 { get; set; } = "";
    [Required] public string RecipientCity { get; set; } = "";
    [Required] public string RecipientPostalCode { get; set; } = "";
    [Required] public string RecipientCountryCode { get; set; } = "IN";
    [Range(0.001, 9999)] public decimal WeightKilograms { get; set; }
    [Required] public string Contents { get; set; } = "";
    public decimal ChargeAmount { get; set; }
    [Required] public string CurrencyCode { get; set; } = "INR";
}
public sealed record BranchOption(Guid Id, string Name);
public sealed class InventoryForm : IBranchSelectionForm { [Required] public Guid BranchId { get; set; } public IReadOnlyList<BranchOption> Branches { get; set; } = []; [Required] public string Sku { get; set; } = ""; [Required] public string Name { get; set; } = ""; [Range(0, 100000)] public int ReorderLevel { get; set; } }
