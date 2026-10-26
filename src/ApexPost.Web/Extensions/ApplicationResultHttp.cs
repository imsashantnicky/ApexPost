using ApexPost.Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace ApexPost.Web.Extensions;

public static class ApplicationResultHttp
{
    public static IActionResult Failure(ControllerBase controller, IReadOnlyList<ValidationError> errors)
    {
        var code = errors.Count > 0 ? errors[0].Code : string.Empty;
        var status = code switch
        {
            "actor.required" or "actor.not-found" => StatusCodes.Status401Unauthorized,
            "actor.inactive" or "access.denied" => StatusCodes.Status403Forbidden,
            "branch.not-found" or "resource.not-found" => StatusCodes.Status404NotFound,
            "branch.closed" or "concurrency.conflict" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        var title = status switch
        {
            StatusCodes.Status401Unauthorized => "Authentication is required.",
            StatusCodes.Status403Forbidden => "The operation is not permitted.",
            StatusCodes.Status404NotFound => "The requested resource is unavailable.",
            StatusCodes.Status409Conflict => "The operation conflicts with current state.",
            _ => "The request is invalid."
        };
        return controller.StatusCode(status, new ProblemDetails { Status = status, Title = title });
    }
}
