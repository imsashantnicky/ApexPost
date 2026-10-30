using System.Security.Claims;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Staff;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Security;

public sealed class ValidatedStaffRequirement : IAuthorizationRequirement;

/// <summary>
/// Confirms the signed-in account still maps to active staff at the claimed branch with a compatible role.
/// Runs for every policy evaluation (the layout alone evaluates four), so the lookup goes through
/// <see cref="StaffAccessCache"/> instead of reading staff.json each time.
/// </summary>
public sealed class ValidatedStaffHandler(StaffAccessCache staffAccess, IHttpContextAccessor contextAccessor) : AuthorizationHandler<ValidatedStaffRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ValidatedStaffRequirement requirement)
    {
        var staffClaim = context.User.FindFirstValue(ActorClaimTypes.StaffId);
        var branchClaim = context.User.FindFirstValue(ActorClaimTypes.AssignedBranchId);
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (!Guid.TryParse(staffClaim, out var staffId) || staffId == Guid.Empty || !Guid.TryParse(branchClaim, out var branchId) || branchId == Guid.Empty) return;

        var staff = await staffAccess.GetAsync(staffId, contextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        if (staff is null || staff.Status != EmploymentStatus.Active || staff.BranchId != branchId) return;

        if (StaffRoleMapping.MatchesStaff(role, staff.Roles)) context.Succeed(requirement);
    }
}
