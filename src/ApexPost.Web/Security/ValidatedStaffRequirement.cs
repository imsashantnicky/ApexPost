using System.Security.Claims;
using ApexPost.Application.Security;
using ApexPost.Application.Staff;
using ApexPost.Domain.Common;
using ApexPost.Domain.Staff;
using ApexPost.Web.Services;
using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Security;

public sealed class ValidatedStaffRequirement : IAuthorizationRequirement;

public sealed class ValidatedStaffHandler(IStaffRepository staffRepository, IHttpContextAccessor contextAccessor) : AuthorizationHandler<ValidatedStaffRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ValidatedStaffRequirement requirement)
    {
        var staffClaim = context.User.FindFirstValue(ActorClaimTypes.StaffId);
        var branchClaim = context.User.FindFirstValue(ActorClaimTypes.AssignedBranchId);
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (!Guid.TryParse(staffClaim, out var staffId) || staffId == Guid.Empty || !Guid.TryParse(branchClaim, out var branchId) || branchId == Guid.Empty) return;

        var staff = await staffRepository.GetByIdAsync(EntityId.From(staffId), contextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        if (staff is null || staff.Value.Status != EmploymentStatus.Active || staff.Value.BranchId.Value != branchId) return;

        if (StaffRoleMapping.MatchesStaff(role, staff.Value.Roles)) context.Succeed(requirement);
    }
}
