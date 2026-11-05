using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jff_agent_infra.Migrations
{
    /// <inheritdoc />
    public partial class parametersAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Arguments",
                table: "Agents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Command",
                table: "Agents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Agents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Arguments",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Command",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Agents");
        }
    }
}
