using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Claims.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReserve : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ReserveAmount",
                table: "claims",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReserveAmount",
                table: "claims");
        }
    }
}
