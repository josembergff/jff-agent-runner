using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Infra.Context;

#nullable disable

namespace jff_agent_infra.Migrations;

[DbContext(typeof(JffAgentDbContext))]
[Migration("20261114120000_AddConfigurations")]
public partial class AddConfigurations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name: "Configurations",
        columns: table => new
        {
            Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
            Username = table.Column<string>(type: "TEXT", nullable: false),
            PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
            CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
            CreatorUserId = table.Column<int>(type: "INTEGER", nullable: false),
            UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
        },
        constraints: table => table.PrimaryKey("PK_Configurations", x => x.Id));

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "Configurations");
}
