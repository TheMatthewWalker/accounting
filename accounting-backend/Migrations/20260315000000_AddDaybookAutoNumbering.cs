using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddDaybookAutoNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add ExternalReference column to DaybookEntries
            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "DaybookEntries",
                type: "text",
                nullable: true);

            // Create DaybookSequences table
            migrationBuilder.CreateTable(
                name: "DaybookSequences",
                columns: table => new
                {
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<string>(type: "text", nullable: false),
                    LastNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DaybookSequences", x => new { x.OrganisationId, x.EntryType });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DaybookSequences");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "DaybookEntries");
        }
    }
}
