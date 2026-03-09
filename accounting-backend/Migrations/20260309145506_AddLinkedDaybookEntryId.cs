using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedDaybookEntryId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedDaybookEntryId",
                table: "DaybookEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DaybookEntries_LinkedDaybookEntryId",
                table: "DaybookEntries",
                column: "LinkedDaybookEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DaybookEntries_DaybookEntries_LinkedDaybookEntryId",
                table: "DaybookEntries",
                column: "LinkedDaybookEntryId",
                principalTable: "DaybookEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DaybookEntries_DaybookEntries_LinkedDaybookEntryId",
                table: "DaybookEntries");

            migrationBuilder.DropIndex(
                name: "IX_DaybookEntries_LinkedDaybookEntryId",
                table: "DaybookEntries");

            migrationBuilder.DropColumn(
                name: "LinkedDaybookEntryId",
                table: "DaybookEntries");
        }
    }
}
