using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pressing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "LaundryOrders",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PromoCodeSnapshot",
                table: "LaundryOrders",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromotionId",
                table: "LaundryOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GarmentCategoryId = table.Column<int>(type: "integer", nullable: true),
                    StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                    table.CheckConstraint("CK_Promotions_Value_Positive", "\"Value\" > 0");
                    table.ForeignKey(
                        name: "FK_Promotions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Promotions_GarmentCategories_GarmentCategoryId",
                        column: x => x.GarmentCategoryId,
                        principalTable: "GarmentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LaundryOrders_PromotionId",
                table: "LaundryOrders",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CreatedByUserId",
                table: "Promotions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_GarmentCategoryId",
                table: "Promotions",
                column: "GarmentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_IsActive_StartsAtUtc_EndsAtUtc",
                table: "Promotions",
                columns: new[] { "IsActive", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_NormalizedCode",
                table: "Promotions",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LaundryOrders_Promotions_PromotionId",
                table: "LaundryOrders",
                column: "PromotionId",
                principalTable: "Promotions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LaundryOrders_Promotions_PromotionId",
                table: "LaundryOrders");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_LaundryOrders_PromotionId",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "PromoCodeSnapshot",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "PromotionId",
                table: "LaundryOrders");
        }
    }
}
