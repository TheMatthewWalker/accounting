using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationVatSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultVatAccountId",
                table: "Organisations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VatFullRate",
                table: "Organisations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VatReducedRate",
                table: "Organisations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_DefaultVatAccountId",
                table: "Organisations",
                column: "DefaultVatAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Organisations_GLAccounts_DefaultVatAccountId",
                table: "Organisations",
                column: "DefaultVatAccountId",
                principalTable: "GLAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Organisations_GLAccounts_DefaultVatAccountId",
                table: "Organisations");

            migrationBuilder.DropIndex(
                name: "IX_Organisations_DefaultVatAccountId",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "DefaultVatAccountId",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "VatFullRate",
                table: "Organisations");

            migrationBuilder.DropColumn(
                name: "VatReducedRate",
                table: "Organisations");
        }
    }
}
