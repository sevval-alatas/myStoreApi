using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using MyStore.Api.Authorization.Requirements;
using MyStore.Api.Enums;

public class MarketAuthorizationHandler :
    AuthorizationHandler<IAuthorizationRequirement>
{
    private readonly IMarketAuthorizationService _service;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MarketAuthorizationHandler(
        IMarketAuthorizationService service,
        IHttpContextAccessor httpContextAccessor)
    {
        _service = service;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IAuthorizationRequirement requirement)
    {
        var userId = context.User.FindFirst("UserId")?.Value;
        if (userId == null) return;

        var httpContext = _httpContextAccessor.HttpContext!;
        var marketId = httpContext.Request.RouteValues["marketId"]?.ToString();
        if (marketId == null) return;

        var role = await _service.GetUserRoleAsync(userId, marketId);
        if (role == null) return;

        if (requirement is OwnerOnlyRequirement && role == MarketRole.Owner)
            context.Succeed(requirement);

        if (requirement is MemberOrOwnerRequirement &&
            (role == MarketRole.Owner || role == MarketRole.Member))
            context.Succeed(requirement);

        if (requirement is ViewerOrAboveRequirement)
            context.Succeed(requirement);
    }
}