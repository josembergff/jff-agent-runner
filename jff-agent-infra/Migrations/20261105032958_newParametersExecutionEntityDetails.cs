using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jff_agent_infra.Migrations
{
    /// <inheritdoc />
    public partial class newParametersExecutionEntityDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionDetails",
                table: "Executions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SessionDetails",
                table: "Executions");
        }
    }
}
