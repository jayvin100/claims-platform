using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PickUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedOfficerId",
                table: "claims",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "claims",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_claims_AssignedOfficerId",
                table: "claims",
                column: "AssignedOfficerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_claims_AssignedOfficerId",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "AssignedOfficerId",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "claims");
        }
    }
}
