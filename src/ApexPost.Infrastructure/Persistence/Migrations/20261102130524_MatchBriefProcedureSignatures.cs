using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives the procedures the brief's signatures. The measured period report moves, unchanged, to
    /// usp_DailyBranchSalesRange; usp_DailyBranchSales becomes its one-day wrapper (@BranchCode, @Date), and
    /// usp_AdjustStock takes @Delta and @ChangedBy. The range procedure is created first, so the wrapper finds it.
    /// </summary>
    public partial class MatchBriefProcedureSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ProcedureScripts.Deploy(ProcedureScripts.DailyBranchSalesRange));
            migrationBuilder.Sql(ProcedureScripts.Deploy(ProcedureScripts.DailyBranchSales));
            migrationBuilder.Sql(ProcedureScripts.Deploy(ProcedureScripts.AdjustStock));
        }

        /// <summary>
        /// Procedure migrations deploy the scripts in sql/procedures as they are now, so they only roll forward
        /// (ADR-012). Going back removes the range procedure that this migration added.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ProcedureScripts.Drop(ProcedureScripts.DailyBranchSalesRange));
        }
    }
}
