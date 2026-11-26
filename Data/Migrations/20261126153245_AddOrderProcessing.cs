using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pressing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LaundryOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CustomerId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedReadyAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Express = table.Column<bool>(type: "boolean", nullable: false),
                    HomeCollection = table.Column<bool>(type: "boolean", nullable: false),
                    HomeDelivery = table.Column<bool>(type: "boolean", nullable: false),
                    ExpressSurcharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectionFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LaundryOrders", x => x.Id);
                    table.CheckConstraint("CK_LaundryOrders_AmountPaid_NonNegative", "\"AmountPaid\" >= 0");
                    table.CheckConstraint("CK_LaundryOrders_Total_NonNegative", "\"TotalAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_LaundryOrders_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LaundryOrders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LaundryOrderId = table.Column<int>(type: "integer", nullable: false),
                    ServiceTariffId = table.Column<int>(type: "integer", nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Color = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Brand = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ReceptionCondition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_LaundryOrders_LaundryOrderId",
                        column: x => x.LaundryOrderId,
                        principalTable: "LaundryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderLines_ServiceTariffs_ServiceTariffId",
                        column: x => x.ServiceTariffId,
                        principalTable: "ServiceTariffs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LaundryOrderId = table.Column<int>(type: "integer", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistories_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistories_LaundryOrders_LaundryOrderId",
                        column: x => x.LaundryOrderId,
                        principalTable: "LaundryOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_CreatedAtUtc",
                table: "LaundryOrders",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_CreatedByUserId",
                table: "LaundryOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_CustomerId",
                table: "LaundryOrders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_Number",
                table: "LaundryOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_Status",
                table: "LaundryOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_LaundryOrderId",
                table: "OrderLines",
                column: "LaundryOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_ServiceTariffId",
                table: "OrderLines",
                column: "ServiceTariffId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_LaundryOrderId_ChangedAtUtc",
                table: "OrderStatusHistories",
                columns: new[] { "LaundryOrderId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_UserId",
                table: "OrderStatusHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "OrderStatusHistories");

            migrationBuilder.DropTable(
                name: "LaundryOrders");
        }
    }
}
