using Microsoft.AspNetCore.Authorization;

namespace MyStore.Api.Authorization.Requirements;

public partial class MemberOrOwnerRequirement : IAuthorizationRequirement
{
}