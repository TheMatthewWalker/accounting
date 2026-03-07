using System.Security.Claims;
using AccountingApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace AccountingApp.Filters;

/// <summary>
/// Decorates a controller action to require a minimum organisation role.
/// Role hierarchy (ascending): Viewer → Bookkeeper → Manager → Owner
/// </summary>
public class RequireOrganisationRoleAttribute : TypeFilterAttribute
{
    public RequireOrganisationRoleAttribute(string minimumRole)
        : base(typeof(OrganisationRoleFilter))
    {
        Arguments = new object[] { minimumRole };
    }
}

/// <summary>
/// Action filter that enforces organisation-level role-based access control.
/// Reads organisationId from route data (tries "organisationId" then "id").
/// Returns 401 if unauthenticated, 403 if the user lacks the required role.
/// </summary>
public class OrganisationRoleFilter : IAsyncActionFilter
{
    private static readonly string[] RoleHierarchy = { "Viewer", "Bookkeeper", "Manager", "Owner" };

    private readonly ApplicationDbContext _context;
    private readonly string _minimumRole;

    public OrganisationRoleFilter(ApplicationDbContext context, string minimumRole)
    {
        _context = context;
        _minimumRole = minimumRole;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Route param is "organisationId" on most controllers, "id" on OrganisationsController
        if (!context.RouteData.Values.TryGetValue("organisationId", out var orgIdObj) &&
            !context.RouteData.Values.TryGetValue("id", out orgIdObj))
        {
            context.Result = new BadRequestObjectResult("Unable to determine organisation from route.");
            return;
        }

        if (!Guid.TryParse(orgIdObj?.ToString(), out var organisationId))
        {
            context.Result = new BadRequestObjectResult("Invalid organisation identifier.");
            return;
        }

        var membership = await _context.OrganisationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganisationId == organisationId && m.IsActive);

        if (membership == null)
        {
            context.Result = new ForbidResult();
            return;
        }

        if (!HasSufficientRole(membership.Role, _minimumRole))
        {
            context.Result = new ObjectResult(new
            {
                success = false,
                error = new
                {
                    code = "FORBIDDEN",
                    message = $"This action requires at least the '{_minimumRole}' role. Your current role is '{membership.Role}'."
                }
            })
            { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }

    private static bool HasSufficientRole(string userRole, string requiredRole)
    {
        var userLevel = Array.IndexOf(RoleHierarchy, userRole);
        var requiredLevel = Array.IndexOf(RoleHierarchy, requiredRole);
        return userLevel >= requiredLevel && userLevel >= 0;
    }
}
