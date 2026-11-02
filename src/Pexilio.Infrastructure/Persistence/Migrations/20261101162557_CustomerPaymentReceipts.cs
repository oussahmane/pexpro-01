using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pexilio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerPaymentReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "pexilio",
                table: "sale_payment",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                schema: "pexilio",
                table: "sale_payment",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "pexilio",
                table: "sale_payment",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "document_counter",
                schema: "pexilio",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Series = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_counter", x => new { x.CompanyId, x.Series });
                    table.CheckConstraint("CK_document_counter_value", "\"LastValue\" > 0");
                    table.ForeignKey(
                        name: "FK_document_counter_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_sale_payment_company_receipt",
                schema: "pexilio",
                table: "sale_payment",
                columns: new[] { "CompanyId", "ReceiptNumber" },
                unique: true,
                filter: "\"ReceiptNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_counter",
                schema: "pexilio");

            migrationBuilder.DropIndex(
                name: "UX_sale_payment_company_receipt",
                schema: "pexilio",
                table: "sale_payment");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "pexilio",
                table: "sale_payment");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                schema: "pexilio",
                table: "sale_payment");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "pexilio",
                table: "sale_payment");
        }
    }
}
