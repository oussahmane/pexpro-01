using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pexilio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchasingDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "purchase_number_seq",
                schema: "pexilio");

            migrationBuilder.CreateTable(
                name: "purchase_idempotency",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_idempotency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_idempotency_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplier",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier", x => x.Id);
                    table.CheckConstraint("CK_supplier_name", "char_length(btrim(\"Name\")) > 0 AND char_length(\"Name\") <= 160");
                    table.ForeignKey(
                        name: "FK_supplier_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SupplierTaxId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SupplierDocumentRef = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SupplierDocumentKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrderedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                    // xmin is PostgreSQL's system column and the purchase concurrency token. It is not created.
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase", x => x.Id);
                    table.CheckConstraint("CK_purchase_document", "(\"SupplierDocumentRef\" IS NULL AND \"SupplierDocumentKey\" IS NULL)\nOR (\"SupplierDocumentRef\" IS NOT NULL AND \"SupplierDocumentKey\" IS NOT NULL)");
                    table.CheckConstraint("CK_purchase_status", "\"Status\" IN ('draft', 'ordered', 'partially_received', 'received', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_purchase_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "pexilio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_supplier_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "pexilio",
                        principalTable: "supplier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_line",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOrdered = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    QuantityReceived = table.Column<decimal>(type: "numeric(19,4)", nullable: false, defaultValue: 0m),
                    UnitCost = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    ProductSku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_line", x => x.Id);
                    table.CheckConstraint("CK_purchase_line_amounts", "\"QuantityOrdered\" > 0\nAND \"QuantityReceived\" >= 0\nAND \"QuantityReceived\" <= \"QuantityOrdered\"\nAND \"UnitCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_purchase_line_product_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pexilio",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_line_purchase_PurchaseId",
                        column: x => x.PurchaseId,
                        principalSchema: "pexilio",
                        principalTable: "purchase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_CreatedByUserId",
                schema: "pexilio",
                table: "purchase",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_SupplierId",
                schema: "pexilio",
                table: "purchase",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "UX_purchase_company_number",
                schema: "pexilio",
                table: "purchase",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_purchase_supplier_document",
                schema: "pexilio",
                table: "purchase",
                columns: new[] { "CompanyId", "SupplierId", "SupplierDocumentKey" },
                unique: true,
                filter: "\"SupplierDocumentKey\" IS NOT NULL AND \"Status\" <> 'cancelled'");

            migrationBuilder.CreateIndex(
                name: "UX_purchase_idempotency_company_operation_key",
                schema: "pexilio",
                table: "purchase_idempotency",
                columns: new[] { "CompanyId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_line_ProductId",
                schema: "pexilio",
                table: "purchase_line",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_line_PurchaseId_LineNumber",
                schema: "pexilio",
                table: "purchase_line",
                columns: new[] { "PurchaseId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_line_PurchaseId_ProductId",
                schema: "pexilio",
                table: "purchase_line",
                columns: new[] { "PurchaseId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_supplier_company_name",
                schema: "pexilio",
                table: "supplier",
                columns: new[] { "CompanyId", "NameKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_idempotency",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "purchase_line",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "purchase",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "supplier",
                schema: "pexilio");

            migrationBuilder.DropSequence(
                name: "purchase_number_seq",
                schema: "pexilio");
        }
    }
}
