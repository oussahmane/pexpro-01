using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pexilio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Stock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_idempotency",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_idempotency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_idempotency_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_location",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    PreventNegativeStock = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_location", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stock_location_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_balance",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    MinimumQuantity = table.Column<decimal>(type: "numeric(19,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_balance", x => x.Id);
                    table.CheckConstraint("CK_stock_balance_minimum", "\"MinimumQuantity\" IS NULL OR \"MinimumQuantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_stock_balance_product_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pexilio",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_balance_stock_location_StockLocationId",
                        column: x => x.StockLocationId,
                        principalSchema: "pexilio",
                        principalTable: "stock_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movement",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,4)", nullable: true),
                    SourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock_movement", x => x.Id);
                    table.CheckConstraint("CK_stock_movement_delta", "\"QuantityDelta\" <> 0\nAND (\n    (\"MovementType\" IN ('opening', 'receipt', 'sale_return') AND \"QuantityDelta\" > 0)\n    OR (\"MovementType\" = 'adjustment' AND \"QuantityDelta\" <> 0)\n    OR (\"MovementType\" = 'sale' AND \"QuantityDelta\" < 0)\n)");
                    table.CheckConstraint("CK_stock_movement_reason", "char_length(btrim(\"Reason\")) > 0 AND char_length(\"Reason\") <= 200");
                    table.CheckConstraint("CK_stock_movement_source", "\"SourceType\" IS NULL OR \"SourceType\" IN ('manual', 'purchase', 'sale', 'sale_return')");
                    table.CheckConstraint("CK_stock_movement_unit_cost", "\"UnitCost\" IS NULL OR \"UnitCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_stock_movement_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "pexilio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movement_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movement_product_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "pexilio",
                        principalTable: "product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movement_stock_location_StockLocationId",
                        column: x => x.StockLocationId,
                        principalSchema: "pexilio",
                        principalTable: "stock_location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_balance_ProductId",
                schema: "pexilio",
                table: "stock_balance",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_stock_balance_location_product",
                schema: "pexilio",
                table: "stock_balance",
                columns: new[] { "StockLocationId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_stock_idempotency_company_key",
                schema: "pexilio",
                table: "stock_idempotency",
                columns: new[] { "CompanyId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_location_CompanyId_Code",
                schema: "pexilio",
                table: "stock_location",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_stock_location_default",
                schema: "pexilio",
                table: "stock_location",
                column: "CompanyId",
                unique: true,
                filter: "\"IsDefault\"");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movement_CompanyId_ProductId_OccurredAtUtc",
                schema: "pexilio",
                table: "stock_movement",
                columns: new[] { "CompanyId", "ProductId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movement_CreatedByUserId",
                schema: "pexilio",
                table: "stock_movement",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movement_ProductId",
                schema: "pexilio",
                table: "stock_movement",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movement_StockLocationId",
                schema: "pexilio",
                table: "stock_movement",
                column: "StockLocationId");

            migrationBuilder.Sql(
                """
                CREATE FUNCTION pexilio.stock_balance_prevent_negative()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF NEW."Quantity" < 0 AND EXISTS (
                    SELECT 1
                    FROM pexilio.stock_location
                    WHERE "Id" = NEW."StockLocationId"
                      AND "PreventNegativeStock"
                  ) THEN
                    RAISE EXCEPTION 'stock.insufficient' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER stock_balance_prevent_negative
                BEFORE INSERT OR UPDATE ON pexilio.stock_balance
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.stock_balance_prevent_negative();

                CREATE FUNCTION pexilio.stock_movement_append_only()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  RAISE EXCEPTION 'stock.movement_immutable' USING ERRCODE = '42501';
                END;
                $$;

                CREATE TRIGGER stock_movement_append_only
                BEFORE UPDATE OR DELETE ON pexilio.stock_movement
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.stock_movement_append_only();

                CREATE TRIGGER stock_movement_append_only_truncate
                BEFORE TRUNCATE ON pexilio.stock_movement
                FOR EACH STATEMENT
                EXECUTE FUNCTION pexilio.stock_movement_append_only();

                CREATE FUNCTION pexilio.stock_movement_matches_balance()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  stored numeric(19,4);
                  summed numeric(19,4);
                BEGIN
                  SELECT "Quantity" INTO stored
                  FROM pexilio.stock_balance
                  WHERE "StockLocationId" = NEW."StockLocationId"
                    AND "ProductId" = NEW."ProductId";

                  SELECT COALESCE(SUM("QuantityDelta"), 0) INTO summed
                  FROM pexilio.stock_movement
                  WHERE "StockLocationId" = NEW."StockLocationId"
                    AND "ProductId" = NEW."ProductId";

                  IF stored IS NULL OR stored IS DISTINCT FROM summed OR NEW."BalanceAfter" IS DISTINCT FROM stored THEN
                    RAISE EXCEPTION 'stock.reconciliation' USING ERRCODE = '23514';
                  END IF;

                  RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER stock_movement_matches_balance
                AFTER INSERT ON pexilio.stock_movement
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.stock_movement_matches_balance();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS stock_movement_matches_balance ON pexilio.stock_movement;
                DROP TRIGGER IF EXISTS stock_movement_append_only_truncate ON pexilio.stock_movement;
                DROP TRIGGER IF EXISTS stock_movement_append_only ON pexilio.stock_movement;
                DROP TRIGGER IF EXISTS stock_balance_prevent_negative ON pexilio.stock_balance;
                DROP FUNCTION IF EXISTS pexilio.stock_movement_matches_balance();
                DROP FUNCTION IF EXISTS pexilio.stock_movement_append_only();
                DROP FUNCTION IF EXISTS pexilio.stock_balance_prevent_negative();
                """);

            migrationBuilder.DropTable(
                name: "stock_balance",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "stock_idempotency",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "stock_movement",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "stock_location",
                schema: "pexilio");
        }
    }
}
