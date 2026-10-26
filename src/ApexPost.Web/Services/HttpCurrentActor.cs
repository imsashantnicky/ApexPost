using System.Security.Claims;
using ApexPost.Application.Security;

namespace ApexPost.Web.Services;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public ActorContext? Actor
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var staffId = principal?.FindFirstValue(ActorClaimTypes.StaffId);
            var branchId = principal?.FindFirstValue(ActorClaimTypes.AssignedBranchId);
            if (!Guid.TryParse(staffId, out var parsedStaffId)) return null;
            Guid? parsedBranchId = Guid.TryParse(branchId, out var parsed) ? parsed : null;
            return new ActorContext(parsedStaffId, principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty, parsedBranchId,
                accessor.HttpContext?.TraceIdentifier ?? string.Empty);
        }
    }
}
