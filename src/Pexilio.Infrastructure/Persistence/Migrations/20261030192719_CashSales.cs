using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pexilio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CashSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "sale_number_seq",
                schema: "pexilio");

            migrationBuilder.CreateSequence(
                name: "sale_return_number_seq",
                schema: "pexilio");

            migrationBuilder.CreateTable(
                name: "sale",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale", x => x.Id);
                    table.UniqueConstraint("AK_sale_id_company", x => new { x.Id, x.CompanyId });
                    table.CheckConstraint("CK_sale_amounts", "\"Subtotal\" >= 0\nAND \"DiscountAmount\" >= 0\nAND \"Total\" >= 0\nAND \"Total\" = \"Subtotal\" - \"DiscountAmount\"");
                    table.CheckConstraint("CK_sale_status", "\"Status\" = 'posted'");
                    table.ForeignKey(
                        name: "FK_sale_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "pexilio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_idempotency",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_idempotency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sale_idempotency_company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "pexilio",
                        principalTable: "company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_idempotency_sale_SaleId",
                        column: x => x.SaleId,
                        principalSchema: "pexilio",
                        principalTable: "sale",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_line",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNumber = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    ListUnitPrice = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    LineDiscount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    AllocatedSaleDiscount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    ProductSku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TracksStock = table.Column<bool>(type: "boolean", nullable: false),
                    TaxRateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_line", x => x.Id);
                    table.UniqueConstraint("AK_sale_line_identity", x => new { x.Id, x.SaleId, x.ProductId });
                    table.CheckConstraint("CK_sale_line_amounts", "\"LineNumber\" > 0\nAND \"Quantity\" > 0\nAND \"ListUnitPrice\" >= 0\nAND \"UnitPrice\" >= 0\nAND \"LineDiscount\" >= 0\nAND \"AllocatedSaleDiscount\" >= 0\nAND \"AllocatedSaleDiscount\" <= \"LineTotal\"\nAND \"LineTotal\" >= 0\nAND \"LineTotal\" = (\"Quantity\" * \"UnitPrice\") - \"LineDiscount\"\nAND \"TaxRate\" >= 0");
                    table.ForeignKey(
                        name: "FK_sale_line_movement",
                        column: x => x.StockMovementId,
                        principalSchema: "pexilio",
                        principalTable: "stock_movement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_line_product",
                        columns: x => new { x.ProductId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "product",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_line_sale",
                        columns: x => new { x.SaleId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "sale",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_line_tax_rate_TaxRateId",
                        column: x => x.TaxRateId,
                        principalSchema: "pexilio",
                        principalTable: "tax_rate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_payment",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_payment", x => x.Id);
                    table.CheckConstraint("CK_sale_payment_amount", "\"Amount\" > 0");
                    table.CheckConstraint("CK_sale_payment_method", "\"Method\" IN ('cash', 'transfer', 'card', 'other')");
                    table.ForeignKey(
                        name: "FK_sale_payment_AspNetUsers_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalSchema: "pexilio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_payment_sale",
                        columns: x => new { x.SaleId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "sale",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_return",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReturnedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReturnedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_return", x => x.Id);
                    table.UniqueConstraint("AK_sale_return_identity", x => new { x.Id, x.SaleId, x.CompanyId });
                    table.CheckConstraint("CK_sale_return_reason", "char_length(btrim(\"Reason\")) > 0 AND char_length(\"Reason\") <= 200");
                    table.ForeignKey(
                        name: "FK_sale_return_AspNetUsers_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalSchema: "pexilio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_return_sale",
                        columns: x => new { x.SaleId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "sale",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_return_line",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReturnId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    TracksStock = table.Column<bool>(type: "boolean", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sale_return_line", x => x.Id);
                    table.CheckConstraint("CK_sale_return_line_amounts", "\"Quantity\" > 0 AND \"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_sale_return_line_movement",
                        column: x => x.StockMovementId,
                        principalSchema: "pexilio",
                        principalTable: "stock_movement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_return_line_return",
                        columns: x => new { x.ReturnId, x.SaleId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "sale_return",
                        principalColumns: new[] { "Id", "SaleId", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sale_return_line_sale_line",
                        columns: x => new { x.SaleLineId, x.SaleId, x.ProductId },
                        principalSchema: "pexilio",
                        principalTable: "sale_line",
                        principalColumns: new[] { "Id", "SaleId", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_stock_movement_sale_source",
                schema: "pexilio",
                table: "stock_movement",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceType\" = 'sale' AND \"SourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_stock_movement_sale_return_source",
                schema: "pexilio",
                table: "stock_movement",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceType\" = 'sale_return' AND \"SourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sale_CompanyId_CreatedAtUtc",
                schema: "pexilio",
                table: "sale",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_CreatedByUserId",
                schema: "pexilio",
                table: "sale",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_sale_company_number",
                schema: "pexilio",
                table: "sale",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_idempotency_SaleId",
                schema: "pexilio",
                table: "sale_idempotency",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "UX_sale_idempotency_company_operation_key",
                schema: "pexilio",
                table: "sale_idempotency",
                columns: new[] { "CompanyId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_ProductId_CompanyId",
                schema: "pexilio",
                table: "sale_line",
                columns: new[] { "ProductId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_SaleId_CompanyId",
                schema: "pexilio",
                table: "sale_line",
                columns: new[] { "SaleId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_SaleId_LineNumber",
                schema: "pexilio",
                table: "sale_line",
                columns: new[] { "SaleId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_line_TaxRateId",
                schema: "pexilio",
                table: "sale_line",
                column: "TaxRateId");

            migrationBuilder.CreateIndex(
                name: "UX_sale_line_movement",
                schema: "pexilio",
                table: "sale_line",
                column: "StockMovementId",
                unique: true,
                filter: "\"StockMovementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_sale_line_sale_product",
                schema: "pexilio",
                table: "sale_line",
                columns: new[] { "SaleId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_payment_ReceivedByUserId",
                schema: "pexilio",
                table: "sale_payment",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_payment_SaleId_CompanyId",
                schema: "pexilio",
                table: "sale_payment",
                columns: new[] { "SaleId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_ReturnedByUserId",
                schema: "pexilio",
                table: "sale_return",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_SaleId_CompanyId",
                schema: "pexilio",
                table: "sale_return",
                columns: new[] { "SaleId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "UX_sale_return_company_number",
                schema: "pexilio",
                table: "sale_return",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_line_ReturnId_SaleId_CompanyId",
                schema: "pexilio",
                table: "sale_return_line",
                columns: new[] { "ReturnId", "SaleId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_sale_return_line_SaleLineId_SaleId_ProductId",
                schema: "pexilio",
                table: "sale_return_line",
                columns: new[] { "SaleLineId", "SaleId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "UX_sale_return_line_movement",
                schema: "pexilio",
                table: "sale_return_line",
                column: "StockMovementId",
                unique: true,
                filter: "\"StockMovementId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_sale_return_line_return_sale_line",
                schema: "pexilio",
                table: "sale_return_line",
                columns: new[] { "ReturnId", "SaleLineId" },
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE pexilio.sale_line ALTER CONSTRAINT "FK_sale_line_movement" DEFERRABLE INITIALLY DEFERRED;
                ALTER TABLE pexilio.sale_return_line ALTER CONSTRAINT "FK_sale_return_line_movement" DEFERRABLE INITIALLY DEFERRED;

                CREATE FUNCTION pexilio.sale_document_immutable()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  RAISE EXCEPTION 'sale.immutable' USING ERRCODE = '42501';
                END;
                $$;

                CREATE FUNCTION pexilio.sale_assert_outstanding(p_sale_id uuid)
                RETURNS void
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  original numeric(19,4);
                  returned numeric(19,4);
                  paid numeric(19,4);
                BEGIN
                  SELECT "Total" INTO original FROM pexilio.sale WHERE "Id" = p_sale_id;
                  IF NOT FOUND THEN
                    RAISE EXCEPTION 'sale.payment_integrity' USING ERRCODE = '23514';
                  END IF;

                  SELECT COALESCE(SUM("Amount"), 0) INTO returned
                  FROM pexilio.sale_return_line
                  WHERE "SaleId" = p_sale_id;

                  SELECT COALESCE(SUM("Amount"), 0) INTO paid
                  FROM pexilio.sale_payment
                  WHERE "SaleId" = p_sale_id;

                  IF returned > original OR paid > original - returned THEN
                    RAISE EXCEPTION 'sale.payment_integrity' USING ERRCODE = '23514';
                  END IF;
                END;
                $$;

                CREATE FUNCTION pexilio.sale_outstanding_on_insert()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF TG_TABLE_NAME = 'sale' THEN
                    PERFORM pexilio.sale_assert_outstanding(NEW."Id");
                  ELSE
                    PERFORM pexilio.sale_assert_outstanding(NEW."SaleId");
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE FUNCTION pexilio.sale_totals_match_lines()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  subtotal numeric(19,4);
                  allocated numeric(19,4);
                BEGIN
                  SELECT COALESCE(SUM("LineTotal"), 0), COALESCE(SUM("AllocatedSaleDiscount"), 0)
                  INTO subtotal, allocated
                  FROM pexilio.sale_line
                  WHERE "SaleId" = NEW."Id";

                  IF subtotal IS DISTINCT FROM NEW."Subtotal"
                     OR allocated IS DISTINCT FROM NEW."DiscountAmount" THEN
                    RAISE EXCEPTION 'sale.total_integrity' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE FUNCTION pexilio.sale_line_matches_movement()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  movement_count integer;
                  source_type character varying(32);
                  source_id uuid;
                  company_id uuid;
                  product_id uuid;
                  quantity_delta numeric(19,4);
                  movement_type character varying(32);
                BEGIN
                  SELECT COUNT(*) INTO movement_count
                  FROM pexilio.stock_movement
                  WHERE "SourceType" = 'sale' AND "SourceId" = NEW."Id";

                  IF NOT NEW."TracksStock" THEN
                    IF movement_count <> 0 OR NEW."StockMovementId" IS NOT NULL THEN
                      RAISE EXCEPTION 'sale.movement_integrity' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                  END IF;

                  IF movement_count <> 1 OR NEW."StockMovementId" IS NULL THEN
                    RAISE EXCEPTION 'sale.movement_integrity' USING ERRCODE = '23514';
                  END IF;

                  SELECT "SourceType", "SourceId", "CompanyId", "ProductId", "QuantityDelta", "MovementType"
                  INTO source_type, source_id, company_id, product_id, quantity_delta, movement_type
                  FROM pexilio.stock_movement
                  WHERE "Id" = NEW."StockMovementId";

                  IF NOT FOUND
                     OR source_type IS DISTINCT FROM 'sale'
                     OR source_id IS DISTINCT FROM NEW."Id"
                     OR company_id IS DISTINCT FROM NEW."CompanyId"
                     OR product_id IS DISTINCT FROM NEW."ProductId"
                     OR movement_type IS DISTINCT FROM 'sale'
                     OR quantity_delta IS DISTINCT FROM -NEW."Quantity" THEN
                    RAISE EXCEPTION 'sale.movement_integrity' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE FUNCTION pexilio.sale_return_line_matches_movement()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  movement_count integer;
                  source_type character varying(32);
                  source_id uuid;
                  company_id uuid;
                  product_id uuid;
                  quantity_delta numeric(19,4);
                  movement_type character varying(32);
                BEGIN
                  SELECT COUNT(*) INTO movement_count
                  FROM pexilio.stock_movement
                  WHERE "SourceType" = 'sale_return' AND "SourceId" = NEW."Id";

                  IF NOT NEW."TracksStock" THEN
                    IF movement_count <> 0 OR NEW."StockMovementId" IS NOT NULL THEN
                      RAISE EXCEPTION 'sale.return_movement_integrity' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                  END IF;

                  IF movement_count <> 1 OR NEW."StockMovementId" IS NULL THEN
                    RAISE EXCEPTION 'sale.return_movement_integrity' USING ERRCODE = '23514';
                  END IF;

                  SELECT "SourceType", "SourceId", "CompanyId", "ProductId", "QuantityDelta", "MovementType"
                  INTO source_type, source_id, company_id, product_id, quantity_delta, movement_type
                  FROM pexilio.stock_movement
                  WHERE "Id" = NEW."StockMovementId";

                  IF NOT FOUND
                     OR source_type IS DISTINCT FROM 'sale_return'
                     OR source_id IS DISTINCT FROM NEW."Id"
                     OR company_id IS DISTINCT FROM NEW."CompanyId"
                     OR product_id IS DISTINCT FROM NEW."ProductId"
                     OR movement_type IS DISTINCT FROM 'sale_return'
                     OR quantity_delta IS DISTINCT FROM NEW."Quantity" THEN
                    RAISE EXCEPTION 'sale.return_movement_integrity' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE FUNCTION pexilio.sale_return_line_within_sale()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  sold numeric(19,4);
                  net numeric(19,4);
                  tracks boolean;
                  quantity_sum numeric(19,4);
                  amount_sum numeric(19,4);
                BEGIN
                  SELECT "Quantity", "LineTotal" - "AllocatedSaleDiscount", "TracksStock"
                  INTO sold, net, tracks
                  FROM pexilio.sale_line
                  WHERE "Id" = NEW."SaleLineId";

                  IF NOT FOUND OR NEW."TracksStock" IS DISTINCT FROM tracks THEN
                    RAISE EXCEPTION 'sale.return_movement_integrity' USING ERRCODE = '23514';
                  END IF;

                  SELECT COALESCE(SUM("Quantity"), 0), COALESCE(SUM("Amount"), 0)
                  INTO quantity_sum, amount_sum
                  FROM pexilio.sale_return_line
                  WHERE "SaleLineId" = NEW."SaleLineId";

                  IF quantity_sum > sold OR amount_sum > net THEN
                    RAISE EXCEPTION 'sale.return_movement_integrity' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE FUNCTION pexilio.stock_movement_sale_source_matches_line()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  tracks boolean;
                  company_id uuid;
                  product_id uuid;
                  quantity numeric(19,4);
                BEGIN
                  IF NEW."SourceType" IS DISTINCT FROM 'sale' THEN
                    RETURN NULL;
                  END IF;

                  SELECT "TracksStock", "CompanyId", "ProductId", "Quantity"
                  INTO tracks, company_id, product_id, quantity
                  FROM pexilio.sale_line
                  WHERE "Id" = NEW."SourceId";

                  IF NOT FOUND
                     OR tracks IS DISTINCT FROM TRUE
                     OR company_id IS DISTINCT FROM NEW."CompanyId"
                     OR product_id IS DISTINCT FROM NEW."ProductId"
                     OR NEW."MovementType" IS DISTINCT FROM 'sale'
                     OR NEW."QuantityDelta" IS DISTINCT FROM -quantity THEN
                    RAISE EXCEPTION 'sale.movement_integrity' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE FUNCTION pexilio.stock_movement_sale_return_source_matches_line()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  tracks boolean;
                  company_id uuid;
                  product_id uuid;
                  quantity numeric(19,4);
                BEGIN
                  IF NEW."SourceType" IS DISTINCT FROM 'sale_return' THEN
                    RETURN NULL;
                  END IF;

                  SELECT "TracksStock", "CompanyId", "ProductId", "Quantity"
                  INTO tracks, company_id, product_id, quantity
                  FROM pexilio.sale_return_line
                  WHERE "Id" = NEW."SourceId";

                  IF NOT FOUND
                     OR tracks IS DISTINCT FROM TRUE
                     OR company_id IS DISTINCT FROM NEW."CompanyId"
                     OR product_id IS DISTINCT FROM NEW."ProductId"
                     OR NEW."MovementType" IS DISTINCT FROM 'sale_return'
                     OR NEW."QuantityDelta" IS DISTINCT FROM quantity THEN
                    RAISE EXCEPTION 'sale.return_movement_integrity' USING ERRCODE = '23514';
                  END IF;
                  RETURN NULL;
                END;
                $$;

                CREATE TRIGGER sale_append_only
                BEFORE UPDATE OR DELETE ON pexilio.sale
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_append_only_truncate
                BEFORE TRUNCATE ON pexilio.sale
                FOR EACH STATEMENT EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_line_append_only
                BEFORE UPDATE OR DELETE ON pexilio.sale_line
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_line_append_only_truncate
                BEFORE TRUNCATE ON pexilio.sale_line
                FOR EACH STATEMENT EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_payment_append_only
                BEFORE UPDATE OR DELETE ON pexilio.sale_payment
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_payment_append_only_truncate
                BEFORE TRUNCATE ON pexilio.sale_payment
                FOR EACH STATEMENT EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_return_append_only
                BEFORE UPDATE OR DELETE ON pexilio.sale_return
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_return_append_only_truncate
                BEFORE TRUNCATE ON pexilio.sale_return
                FOR EACH STATEMENT EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_return_line_append_only
                BEFORE UPDATE OR DELETE ON pexilio.sale_return_line
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_document_immutable();
                CREATE TRIGGER sale_return_line_append_only_truncate
                BEFORE TRUNCATE ON pexilio.sale_return_line
                FOR EACH STATEMENT EXECUTE FUNCTION pexilio.sale_document_immutable();

                CREATE CONSTRAINT TRIGGER sale_totals_match_lines
                AFTER INSERT ON pexilio.sale
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_totals_match_lines();
                CREATE CONSTRAINT TRIGGER sale_outstanding_on_sale
                AFTER INSERT ON pexilio.sale
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_outstanding_on_insert();
                CREATE CONSTRAINT TRIGGER sale_outstanding_on_payment
                AFTER INSERT ON pexilio.sale_payment
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_outstanding_on_insert();
                CREATE CONSTRAINT TRIGGER sale_outstanding_on_return_line
                AFTER INSERT ON pexilio.sale_return_line
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_outstanding_on_insert();
                CREATE CONSTRAINT TRIGGER sale_line_matches_movement
                AFTER INSERT ON pexilio.sale_line
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_line_matches_movement();
                CREATE CONSTRAINT TRIGGER sale_return_line_matches_movement
                AFTER INSERT ON pexilio.sale_return_line
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_return_line_matches_movement();
                CREATE CONSTRAINT TRIGGER sale_return_line_within_sale
                AFTER INSERT ON pexilio.sale_return_line
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.sale_return_line_within_sale();
                CREATE CONSTRAINT TRIGGER stock_movement_sale_source_matches_line
                AFTER INSERT ON pexilio.stock_movement
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.stock_movement_sale_source_matches_line();
                CREATE CONSTRAINT TRIGGER stock_movement_sale_return_source_matches_line
                AFTER INSERT ON pexilio.stock_movement
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION pexilio.stock_movement_sale_return_source_matches_line();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS stock_movement_sale_return_source_matches_line ON pexilio.stock_movement;
                DROP TRIGGER IF EXISTS stock_movement_sale_source_matches_line ON pexilio.stock_movement;
                DROP TRIGGER IF EXISTS sale_return_line_within_sale ON pexilio.sale_return_line;
                DROP TRIGGER IF EXISTS sale_return_line_matches_movement ON pexilio.sale_return_line;
                DROP TRIGGER IF EXISTS sale_outstanding_on_return_line ON pexilio.sale_return_line;
                DROP TRIGGER IF EXISTS sale_return_line_append_only_truncate ON pexilio.sale_return_line;
                DROP TRIGGER IF EXISTS sale_return_line_append_only ON pexilio.sale_return_line;
                DROP TRIGGER IF EXISTS sale_return_append_only_truncate ON pexilio.sale_return;
                DROP TRIGGER IF EXISTS sale_return_append_only ON pexilio.sale_return;
                DROP TRIGGER IF EXISTS sale_outstanding_on_payment ON pexilio.sale_payment;
                DROP TRIGGER IF EXISTS sale_payment_append_only_truncate ON pexilio.sale_payment;
                DROP TRIGGER IF EXISTS sale_payment_append_only ON pexilio.sale_payment;
                DROP TRIGGER IF EXISTS sale_line_matches_movement ON pexilio.sale_line;
                DROP TRIGGER IF EXISTS sale_line_append_only_truncate ON pexilio.sale_line;
                DROP TRIGGER IF EXISTS sale_line_append_only ON pexilio.sale_line;
                DROP TRIGGER IF EXISTS sale_outstanding_on_sale ON pexilio.sale;
                DROP TRIGGER IF EXISTS sale_totals_match_lines ON pexilio.sale;
                DROP TRIGGER IF EXISTS sale_append_only_truncate ON pexilio.sale;
                DROP TRIGGER IF EXISTS sale_append_only ON pexilio.sale;
                DROP FUNCTION IF EXISTS pexilio.stock_movement_sale_return_source_matches_line();
                DROP FUNCTION IF EXISTS pexilio.stock_movement_sale_source_matches_line();
                DROP FUNCTION IF EXISTS pexilio.sale_return_line_within_sale();
                DROP FUNCTION IF EXISTS pexilio.sale_return_line_matches_movement();
                DROP FUNCTION IF EXISTS pexilio.sale_line_matches_movement();
                DROP FUNCTION IF EXISTS pexilio.sale_totals_match_lines();
                DROP FUNCTION IF EXISTS pexilio.sale_outstanding_on_insert();
                DROP FUNCTION IF EXISTS pexilio.sale_assert_outstanding(uuid);
                DROP FUNCTION IF EXISTS pexilio.sale_document_immutable();
                """);

            migrationBuilder.DropTable(
                name: "sale_idempotency",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "sale_payment",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "sale_return_line",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "sale_return",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "sale_line",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "sale",
                schema: "pexilio");

            migrationBuilder.DropIndex(
                name: "UX_stock_movement_sale_source",
                schema: "pexilio",
                table: "stock_movement");

            migrationBuilder.DropIndex(
                name: "UX_stock_movement_sale_return_source",
                schema: "pexilio",
                table: "stock_movement");

            migrationBuilder.DropSequence(
                name: "sale_number_seq",
                schema: "pexilio");

            migrationBuilder.DropSequence(
                name: "sale_return_number_seq",
                schema: "pexilio");
        }
    }
}
