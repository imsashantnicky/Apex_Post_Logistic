using ApexPost.Application.Abstractions;
using ApexPost.Application.Exceptions;
using ApexPost.Domain.Branches;
using ApexPost.Domain.Personnel;

namespace ApexPost.Application.Extensions;

/// <summary>Role and branch-ownership checks shared by the services; a failed check throws Forbidden.</summary>
public static class CurrentUserExtensions
{
    extension(ICurrentUser user)
    {
        /// <summary>Auditors and administrators work across branches; clerks and managers only in their own.</summary>
        public bool SeesAllBranches => user.Role is StaffRole.Auditor or StaffRole.Administrator;

        public bool CanTransact => user.Role is StaffRole.CounterClerk or StaffRole.BranchManager;

        public void RequireRole(params ReadOnlySpan<StaffRole> roles)
        {
            if (!roles.Contains(user.Role))
            {
                throw new ForbiddenException($"The {user.Role} role cannot do this.");
            }
        }

        /// <summary>Throws unless the user may see data of the branch.</summary>
        public void RequireBranch(BranchCode branch)
        {
            if (!user.SeesAllBranches && user.Branch != branch)
            {
                throw new ForbiddenException($"Staff of branch {user.Branch} cannot access branch {branch}.");
            }
        }

        /// <summary>
        /// The branch a list or report is limited to: what was asked for (null = all branches) for auditors and
        /// administrators, otherwise the user's own branch.
        /// </summary>
        public BranchCode? ScopeTo(BranchCode? requested)
        {
            if (user.SeesAllBranches)
            {
                return requested;
            }

            if (requested is not null)
            {
                user.RequireBranch(requested);
            }

            return user.Branch;
        }
    }
}
