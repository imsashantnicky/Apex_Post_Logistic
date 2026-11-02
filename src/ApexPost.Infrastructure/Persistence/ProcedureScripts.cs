using System.Reflection;

namespace ApexPost.Infrastructure.Persistence;

/// <summary>
/// The stored procedures live as plain files in sql/procedures, where they can be read and run by hand. They are
/// embedded here so that a migration deploys exactly those files.
/// </summary>
internal static class ProcedureScripts
{
    public const string DailyBranchSales = "usp_DailyBranchSales";
    public const string LowStockReport = "usp_LowStockReport";
    public const string AdjustStock = "usp_AdjustStock";

    /// <summary>
    /// The script inside EXEC: an idempotent migration script puts each step in IF ... BEGIN, where CREATE PROCEDURE
    /// is not allowed directly.
    /// </summary>
    public static string Deploy(string procedure)
    {
        var escaped = Read(procedure).Replace("'", "''", StringComparison.Ordinal);
        return $"EXEC(N'{escaped}');";
    }

    private static string Read(string procedure)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ApexPost.Procedures.{procedure}")
            ?? throw new InvalidOperationException($"The script of {procedure} is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string Drop(string procedure) => $"DROP PROCEDURE IF EXISTS dbo.{procedure};";
}
