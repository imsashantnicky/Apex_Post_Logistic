using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Security;

/// <summary>A request naming a branch must name the user's own, unless the user works across branches.</summary>
public sealed class SameBranchRequirement : IAuthorizationRequirement;
