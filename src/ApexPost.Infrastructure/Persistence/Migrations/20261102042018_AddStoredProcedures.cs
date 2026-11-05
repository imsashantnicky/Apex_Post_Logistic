using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ProcedureScripts.Deploy(ProcedureScripts.DailyBranchSales));
            migrationBuilder.Sql(ProcedureScripts.Deploy(ProcedureScripts.LowStockReport));
            migrationBuilder.Sql(ProcedureScripts.Deploy(ProcedureScripts.AdjustStock));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ProcedureScripts.Drop(ProcedureScripts.AdjustStock));
            migrationBuilder.Sql(ProcedureScripts.Drop(ProcedureScripts.LowStockReport));
            migrationBuilder.Sql(ProcedureScripts.Drop(ProcedureScripts.DailyBranchSales));
        }
    }
}
