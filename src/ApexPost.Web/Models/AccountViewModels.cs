using ApexPost.Application.Security;
using System.ComponentModel.DataAnnotations;

namespace ApexPost.Web.Models;
public sealed class BootstrapViewModel { [Required, StringLength(80)] public string UserName { get; set; } = ""; [Required, DataType(DataType.Password), MinLength(12)] public string Password { get; set; } = ""; [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = ""; public Guid StaffId { get; set; } public IReadOnlyList<StaffOption> Staff { get; set; } = []; }
public sealed class LoginViewModel { [Required] public string UserName { get; set; } = ""; [Required, DataType(DataType.Password)] public string Password { get; set; } = ""; }
public sealed class ProvisionAccountViewModel { [Required, StringLength(80), RegularExpression("^[A-Za-z0-9._-]{3,80}$")] public string UserName { get; set; } = ""; [Required, DataType(DataType.Password), MinLength(12)] public string Password { get; set; } = ""; [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = ""; [Required, RegularExpression("^(" + Roles.Administrator + "|" + Roles.BranchManager + "|" + Roles.CounterStaff + "|" + Roles.InventoryStaff + "|" + Roles.Auditor + ")$")] public string Role { get; set; } = Roles.CounterStaff; [Required] public Guid StaffId { get; set; } public IReadOnlyList<StaffOption> Staff { get; set; } = []; }
public sealed record StaffOption(Guid Id, string EmployeeNumber, string Name, string BranchName);
