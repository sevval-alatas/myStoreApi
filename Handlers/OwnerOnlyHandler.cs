using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using MyStore.Api.Authorization.Requirements;
using MyStore.Api.Enums;
using MyStore.Api.Services;

namespace MyStore.Api.Authorization.Handlers;

public class OwnerOnlyHandler : AuthorizationHandler<OwnerOnlyRequirement>
{
    private readonly IMarketAuthorizationService _marketAuth;

    public OwnerOnlyHandler(IMarketAuthorizationService marketAuth)
    {
        _marketAuth = marketAuth;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOnlyRequirement requirement)
    {
        var httpContext = context.Resource as AuthorizationFilterContext;
        if (httpContext == null) return;

        var userId = httpContext.HttpContext.User.FindFirst("UserId")?.Value;
        var marketId = httpContext.RouteData.Values["marketId"]?.ToString();

        if (userId == null || marketId == null) return;

        var role = await _marketAuth.GetUserRoleAsync(userId, marketId);

        if (role == MarketRole.Owner)
        {
            context.Succeed(requirement);
        }
    }
}