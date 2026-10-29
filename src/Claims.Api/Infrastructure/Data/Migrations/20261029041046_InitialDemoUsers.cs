using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Claims.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialDemoUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "demo_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_demo_users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "demo_users",
                columns: new[] { "Id", "Name", "Role" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), "Aisha Rahman", "Claimant" },
                    { new Guid("11111111-0000-0000-0000-000000000002"), "Daniel Tan", "Claimant" },
                    { new Guid("22222222-0000-0000-0000-000000000001"), "Priya Nair", "Officer" },
                    { new Guid("22222222-0000-0000-0000-000000000002"), "Marcus Lim", "Officer" },
                    { new Guid("22222222-0000-0000-0000-000000000003"), "Siti Hassan", "Officer" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "demo_users");
        }
    }
}
