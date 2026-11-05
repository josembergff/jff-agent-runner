using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace jff_agent_infra.Migrations
{
    /// <inheritdoc />
    public partial class parametersExecucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentId",
                table: "Executions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IssueNumber",
                table: "Executions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "Executions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Executions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Executions_AgentId",
                table: "Executions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Executions_ProjectId",
                table: "Executions",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Executions_Agents_AgentId",
                table: "Executions",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Executions_Projects_ProjectId",
                table: "Executions",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Executions_Agents_AgentId",
                table: "Executions");

            migrationBuilder.DropForeignKey(
                name: "FK_Executions_Projects_ProjectId",
                table: "Executions");

            migrationBuilder.DropIndex(
                name: "IX_Executions_AgentId",
                table: "Executions");

            migrationBuilder.DropIndex(
                name: "IX_Executions_ProjectId",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "IssueNumber",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "Executions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Executions");
        }
    }
}
