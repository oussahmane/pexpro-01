using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pexilio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "purchase_receipt_number_seq",
                schema: "pexilio");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_line_identity",
                schema: "pexilio",
                table: "purchase_line",
                columns: new[] { "Id", "PurchaseId", "ProductId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_purchase_id_company",
                schema: "pexilio",
                table: "purchase",
                columns: new[] { "Id", "CompanyId" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_product_id_company",
                schema: "pexilio",
                table: "product",
                columns: new[] { "Id", "CompanyId" });

            migrationBuilder.CreateTable(
                name: "purchase_receipt",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DocumentRef = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DocumentKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_receipt", x => x.Id);
                    table.UniqueConstraint("AK_receipt_identity", x => new { x.Id, x.PurchaseId, x.CompanyId });
                    table.ForeignKey(
                        name: "FK_receipt_purchase",
                        columns: x => new { x.PurchaseId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "purchase",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_user",
                        column: x => x.ReceivedByUserId,
                        principalSchema: "pexilio",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_receipt_line",
                schema: "pexilio",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(19,4)", nullable: false),
                    StockMovementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_receipt_line", x => x.Id);
                    table.CheckConstraint("CK_receipt_line_quantity", "\"Quantity\" > 0 AND \"UnitCost\" >= 0");
                    table.ForeignKey(
                        name: "FK_receipt_line_movement",
                        column: x => x.StockMovementId,
                        principalSchema: "pexilio",
                        principalTable: "stock_movement",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_line_product",
                        columns: x => new { x.ProductId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "product",
                        principalColumns: new[] { "Id", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_line_purchase_line",
                        columns: x => new { x.PurchaseLineId, x.PurchaseId, x.ProductId },
                        principalSchema: "pexilio",
                        principalTable: "purchase_line",
                        principalColumns: new[] { "Id", "PurchaseId", "ProductId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_line_receipt",
                        columns: x => new { x.ReceiptId, x.PurchaseId, x.CompanyId },
                        principalSchema: "pexilio",
                        principalTable: "purchase_receipt",
                        principalColumns: new[] { "Id", "PurchaseId", "CompanyId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_stock_movement_purchase_source",
                schema: "pexilio",
                table: "stock_movement",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "\"SourceType\" = 'purchase' AND \"SourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_PurchaseId_CompanyId",
                schema: "pexilio",
                table: "purchase_receipt",
                columns: new[] { "PurchaseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_ReceivedByUserId",
                schema: "pexilio",
                table: "purchase_receipt",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_receipt_company_number",
                schema: "pexilio",
                table: "purchase_receipt",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_receipt_purchase_document",
                schema: "pexilio",
                table: "purchase_receipt",
                columns: new[] { "PurchaseId", "DocumentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_line_ProductId_CompanyId",
                schema: "pexilio",
                table: "purchase_receipt_line",
                columns: new[] { "ProductId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_line_PurchaseLineId_PurchaseId_ProductId",
                schema: "pexilio",
                table: "purchase_receipt_line",
                columns: new[] { "PurchaseLineId", "PurchaseId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_line_ReceiptId_PurchaseId_CompanyId",
                schema: "pexilio",
                table: "purchase_receipt_line",
                columns: new[] { "ReceiptId", "PurchaseId", "CompanyId" });

            migrationBuilder.CreateIndex(
                name: "UX_receipt_line_movement",
                schema: "pexilio",
                table: "purchase_receipt_line",
                column: "StockMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_receipt_line_receipt_purchase_line",
                schema: "pexilio",
                table: "purchase_receipt_line",
                columns: new[] { "ReceiptId", "PurchaseLineId" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION pexilio.purchase_receipt_line_matches_movement()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  source_type character varying(32);
                  source_id uuid;
                  company_id uuid;
                  product_id uuid;
                  quantity_delta numeric(19,4);
                  unit_cost numeric(19,4);
                  movement_type character varying(32);
                  received numeric(19,4);
                  summed numeric(19,4);
                BEGIN
                  SELECT "SourceType", "SourceId", "CompanyId", "ProductId", "QuantityDelta", "UnitCost", "MovementType"
                  INTO source_type, source_id, company_id, product_id, quantity_delta, unit_cost, movement_type
                  FROM pexilio.stock_movement
                  WHERE "Id" = NEW."StockMovementId";

                  IF NOT FOUND
                     OR source_type IS DISTINCT FROM 'purchase'
                     OR source_id IS DISTINCT FROM NEW."Id"
                     OR company_id IS DISTINCT FROM NEW."CompanyId"
                     OR product_id IS DISTINCT FROM NEW."ProductId"
                     OR quantity_delta IS DISTINCT FROM NEW."Quantity"
                     OR unit_cost IS DISTINCT FROM NEW."UnitCost"
                     OR movement_type IS DISTINCT FROM 'receipt'
                  THEN
                    RAISE EXCEPTION 'purchase.receipt_integrity' USING ERRCODE = '23514';
                  END IF;

                  SELECT "QuantityReceived" INTO received
                  FROM pexilio.purchase_line
                  WHERE "Id" = NEW."PurchaseLineId";

                  SELECT COALESCE(SUM("Quantity"), 0) INTO summed
                  FROM pexilio.purchase_receipt_line
                  WHERE "PurchaseLineId" = NEW."PurchaseLineId";

                  IF received IS NULL OR received IS DISTINCT FROM summed THEN
                    RAISE EXCEPTION 'purchase.receipt_integrity' USING ERRCODE = '23514';
                  END IF;

                  RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER purchase_receipt_line_matches_movement
                AFTER INSERT ON pexilio.purchase_receipt_line
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.purchase_receipt_line_matches_movement();

                CREATE FUNCTION pexilio.purchase_line_received_matches_receipts()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                  summed numeric(19,4);
                BEGIN
                  SELECT COALESCE(SUM("Quantity"), 0) INTO summed
                  FROM pexilio.purchase_receipt_line
                  WHERE "PurchaseLineId" = NEW."Id";

                  IF NEW."QuantityReceived" IS DISTINCT FROM summed THEN
                    RAISE EXCEPTION 'purchase.receipt_integrity' USING ERRCODE = '23514';
                  END IF;

                  RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER purchase_line_received_matches_receipts
                AFTER UPDATE OF "QuantityReceived" ON pexilio.purchase_line
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.purchase_line_received_matches_receipts();

                CREATE FUNCTION pexilio.purchase_line_received_starts_at_zero()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  IF NEW."QuantityReceived" IS DISTINCT FROM 0 THEN
                    RAISE EXCEPTION 'purchase.receipt_integrity' USING ERRCODE = '23514';
                  END IF;
                  RETURN NEW;
                END;
                $$;

                CREATE TRIGGER purchase_line_received_starts_at_zero
                BEFORE INSERT ON pexilio.purchase_line
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.purchase_line_received_starts_at_zero();

                CREATE FUNCTION pexilio.purchase_receipt_append_only()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                  RAISE EXCEPTION 'purchase.receipt_immutable' USING ERRCODE = '42501';
                END;
                $$;

                CREATE TRIGGER purchase_receipt_append_only
                BEFORE UPDATE OR DELETE ON pexilio.purchase_receipt
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.purchase_receipt_append_only();

                CREATE TRIGGER purchase_receipt_append_only_truncate
                BEFORE TRUNCATE ON pexilio.purchase_receipt
                FOR EACH STATEMENT
                EXECUTE FUNCTION pexilio.purchase_receipt_append_only();

                CREATE TRIGGER purchase_receipt_line_append_only
                BEFORE UPDATE OR DELETE ON pexilio.purchase_receipt_line
                FOR EACH ROW
                EXECUTE FUNCTION pexilio.purchase_receipt_append_only();

                CREATE TRIGGER purchase_receipt_line_append_only_truncate
                BEFORE TRUNCATE ON pexilio.purchase_receipt_line
                FOR EACH STATEMENT
                EXECUTE FUNCTION pexilio.purchase_receipt_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS purchase_receipt_append_only_truncate ON pexilio.purchase_receipt;
                DROP TRIGGER IF EXISTS purchase_receipt_append_only ON pexilio.purchase_receipt;
                DROP TRIGGER IF EXISTS purchase_receipt_line_append_only_truncate ON pexilio.purchase_receipt_line;
                DROP TRIGGER IF EXISTS purchase_receipt_line_append_only ON pexilio.purchase_receipt_line;
                DROP TRIGGER IF EXISTS purchase_receipt_line_matches_movement ON pexilio.purchase_receipt_line;
                DROP TRIGGER IF EXISTS purchase_line_received_starts_at_zero ON pexilio.purchase_line;
                DROP TRIGGER IF EXISTS purchase_line_received_matches_receipts ON pexilio.purchase_line;
                DROP FUNCTION IF EXISTS pexilio.purchase_receipt_append_only();
                DROP FUNCTION IF EXISTS pexilio.purchase_line_received_starts_at_zero();
                DROP FUNCTION IF EXISTS pexilio.purchase_receipt_line_matches_movement();
                DROP FUNCTION IF EXISTS pexilio.purchase_line_received_matches_receipts();
                """);

            migrationBuilder.DropTable(
                name: "purchase_receipt_line",
                schema: "pexilio");

            migrationBuilder.DropTable(
                name: "purchase_receipt",
                schema: "pexilio");

            migrationBuilder.DropIndex(
                name: "UX_stock_movement_purchase_source",
                schema: "pexilio",
                table: "stock_movement");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_line_identity",
                schema: "pexilio",
                table: "purchase_line");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_purchase_id_company",
                schema: "pexilio",
                table: "purchase");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_product_id_company",
                schema: "pexilio",
                table: "product");

            migrationBuilder.DropSequence(
                name: "purchase_receipt_number_seq",
                schema: "pexilio");
        }
    }
}
