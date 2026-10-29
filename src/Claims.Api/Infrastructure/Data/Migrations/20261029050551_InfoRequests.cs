using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Claims.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InfoRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "info_requests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_info_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_info_requests_claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_info_requests_ClaimId",
                table: "info_requests",
                column: "ClaimId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "info_requests");
        }
    }
}
