using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pexilio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WholesalePricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BelowMinimumOverride",
                schema: "pexilio",
                table: "sale_line",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DiscountKind",
                schema: "pexilio",
                table: "sale_line",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRate",
                schema: "pexilio",
                table: "sale_line",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceKind",
                schema: "pexilio",
                table: "sale_line",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "retail");

            migrationBuilder.AddColumn<string>(
                name: "DiscountKind",
                schema: "pexilio",
                table: "sale",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountRate",
                schema: "pexilio",
                table: "sale",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumUnitPrice",
                schema: "pexilio",
                table: "product",
                type: "numeric(19,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WholesaleUnitPrice",
                schema: "pexilio",
                table: "product",
                type: "numeric(19,4)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_sale_line_price",
                schema: "pexilio",
                table: "sale_line",
                sql: "\"PriceKind\" IN ('retail', 'wholesale')\nAND (\n    (\"DiscountKind\" = 'none' AND \"LineDiscount\" = 0 AND \"DiscountRate\" IS NULL)\n    OR (\"DiscountKind\" = 'amount' AND \"DiscountRate\" IS NULL)\n    OR (\"DiscountKind\" = 'percent' AND \"DiscountRate\" >= 0 AND \"DiscountRate\" <= 100)\n)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_sale_discount",
                schema: "pexilio",
                table: "sale",
                sql: "(\"DiscountKind\" = 'none' AND \"DiscountAmount\" = 0 AND \"DiscountRate\" IS NULL)\nOR (\"DiscountKind\" = 'amount' AND \"DiscountRate\" IS NULL)\nOR (\"DiscountKind\" = 'percent' AND \"DiscountRate\" >= 0 AND \"DiscountRate\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_product_price_bounds",
                schema: "pexilio",
                table: "product",
                sql: "(\"WholesaleUnitPrice\" IS NULL OR \"WholesaleUnitPrice\" >= 0)\nAND (\"MinimumUnitPrice\" IS NULL OR \"MinimumUnitPrice\" >= 0)\nAND (\"MinimumUnitPrice\" IS NULL OR \"MinimumUnitPrice\" <= \"UnitPrice\")\nAND (\"MinimumUnitPrice\" IS NULL OR \"WholesaleUnitPrice\" IS NULL OR \"MinimumUnitPrice\" <= \"WholesaleUnitPrice\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_sale_line_price",
                schema: "pexilio",
                table: "sale_line");

            migrationBuilder.DropCheckConstraint(
                name: "CK_sale_discount",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropCheckConstraint(
                name: "CK_product_price_bounds",
                schema: "pexilio",
                table: "product");

            migrationBuilder.DropColumn(
                name: "BelowMinimumOverride",
                schema: "pexilio",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "DiscountKind",
                schema: "pexilio",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "DiscountRate",
                schema: "pexilio",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "PriceKind",
                schema: "pexilio",
                table: "sale_line");

            migrationBuilder.DropColumn(
                name: "DiscountKind",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "DiscountRate",
                schema: "pexilio",
                table: "sale");

            migrationBuilder.DropColumn(
                name: "MinimumUnitPrice",
                schema: "pexilio",
                table: "product");

            migrationBuilder.DropColumn(
                name: "WholesaleUnitPrice",
                schema: "pexilio",
                table: "product");
        }
    }
}
