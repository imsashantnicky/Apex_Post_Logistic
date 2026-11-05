using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkloadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_BranchCode",
                table: "CounterTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaidAt",
                table: "Payments",
                column: "PaidAt")
                .Annotation("SqlServer:Include", new[] { "Tax" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchCode_OpenedAt",
                table: "CounterTransactions",
                columns: new[] { "BranchCode", "OpenedAt" })
                .Annotation("SqlServer:Include", new[] { "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_OpenedAt",
                table: "CounterTransactions",
                column: "OpenedAt")
                .Annotation("SqlServer:Include", new[] { "BranchCode", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_PaidAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_BranchCode_OpenedAt",
                table: "CounterTransactions");

            migrationBuilder.DropIndex(
                name: "IX_CounterTransactions_OpenedAt",
                table: "CounterTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_CounterTransactions_BranchCode",
                table: "CounterTransactions",
                column: "BranchCode");
        }
    }
}
