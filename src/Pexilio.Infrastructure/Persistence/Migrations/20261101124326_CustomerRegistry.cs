using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pexilio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "pexilio",
                table: "sale",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                schema: "pexilio",
                table: "sale",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerType",
                schema: "pexilio",
                table: "sale",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Rc = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Nif = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Nis = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Ai = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreditLimit = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.Id);
                    table.UniqueConstraint("AK_customer_id_company", x => new { x.Id, x.CompanyId });
                    table.CheckConstraint("CK_customer_credit_limit", "\"CreditLimit\" IS NULL OR \"CreditLimit\" >= 0");
                    table.CheckConstraint("CK_customer_identifiers", "\"Type\" = 'business'\nOR (\"Rc\" IS NULL AND \"Nif\" IS NULL AND \"Nis\" IS NULL AND \"Ai\" IS NULL)");
                    table.CheckConstraint("CK_customer_name", "char_length(btrim(\"Name\")) > 0 AND char_length(\"Name\") <= 160 AND char_length(\"NameKey\") > 0");
                    table.CheckConstraint("CK_customer_type", "\"Type\" IN ('individual', 'business')");
                    table.ForeignKey(
                        name: "FK_customer_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_idempotency",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_idempotency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_idempotency_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sale_company_customer_created",
                schema: "pexilio",
                table: "sale",
                columns: new[] { "CompanyId", "CustomerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_CustomerId_CompanyId",
                schema: "pexilio",
                table: "sale",
                columns: new[] { "CustomerId", "CompanyId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_sale_customer",
                schema: "pexilio",
                table: "sale",
                sql: "(\"CustomerId\" IS NULL AND \"CustomerName\" IS NULL AND \"CustomerType\" IS NULL)\nOR (\"CustomerId\" IS NOT NULL AND char_length(btrim(\"CustomerName\")) > 0 AND \"CustomerType\" IN ('individual', 'business'))");

            migrationBuilder.CreateIndex(
                name: "IX_customer_company_name_key",
                schema: "pexilio",
                table: "customer",
                columns: new[] { "CompanyId", "NameKey" });

            migrationBuilder.CreateIndex(
                name: "UX_customer_idempotency_company_operation_key",
                schema: "pexilio",
                table: "customer_idempotency",
                columns: new[] { "CompanyId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_sale_customer",
                schema: "pexilio",
                table: "sale",
                columns: new[] { "CustomerId", "CompanyId" },
                principalSchema: "pexilio",
                principalTable: "customer",
                principalColumns: new[] { "Id", "CompanyId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sale_customer",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropTable(
                name: "customer",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "customer_idempotency",
                schema: "pexilio");

            migrationBuilder.DropIndex(
                name: "IX_sale_company_customer_created",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropIndex(
                name: "IX_sale_CustomerId_CompanyId",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sale_customer",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "CustomerType",
                schema: "pexilio",
                table: "sale");
        }
    }
}
