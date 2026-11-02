using System.Data;
using ApexPost.Domain.Branches;
using Microsoft.Data.SqlClient;

namespace ApexPost.Infrastructure.Repositories;

/// <summary>The parameter types shared by the stored procedure calls; they match the column types.</summary>
internal static class ProcedureParameters
{
    public const int BranchCodeLength = 5;
    public const int SkuLength = 8;

    /// <summary>A branch, or a typed NULL for "all branches": EF Core could not type a plain null.</summary>
    public static SqlParameter OptionalBranch(BranchCode? branch) =>
        new("@BranchCode", SqlDbType.VarChar, BranchCodeLength) { Value = (object?)branch?.Value ?? DBNull.Value };
}
