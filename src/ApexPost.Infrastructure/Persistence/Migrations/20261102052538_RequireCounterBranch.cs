using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApexPost.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequireCounterBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Counters_Branch_Number",
                table: "Counters");

            migrationBuilder.AlterColumn<string>(
                name: "BranchCode",
                table: "Counters",
                type: "varchar(5)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(5)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Counters_Branch_Number",
                table: "Counters",
                columns: new[] { "BranchCode", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Counters_Branch_Number",
                table: "Counters");

            migrationBuilder.AlterColumn<string>(
                name: "BranchCode",
                table: "Counters",
                type: "varchar(5)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(5)");

            migrationBuilder.CreateIndex(
                name: "UX_Counters_Branch_Number",
                table: "Counters",
                columns: new[] { "BranchCode", "Number" },
                unique: true,
                filter: "[BranchCode] IS NOT NULL");
        }
    }
}
