using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jff_agent_infra.Migrations
{
    /// <inheritdoc />
    public partial class newParametersExecutionEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinishedAt",
                table: "Executions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SesionAgentCommand",
                table: "Executions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SessionName",
                table: "Executions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SessionProjectPath",
                table: "Executions",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Executions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinishedAt",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "SesionAgentCommand",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "SessionName",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "SessionProjectPath",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Executions");
        }
    }
}
