using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pressing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                table: "InventoryItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContactName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Address = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_SupplierId",
                table: "InventoryItems",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true);

            migrationBuilder.Sql("INSERT INTO \"Suppliers\" (\"Name\", \"IsActive\", \"CreatedAtUtc\") SELECT DISTINCT TRIM(\"Supplier\"), TRUE, CURRENT_TIMESTAMP FROM \"InventoryItems\" WHERE \"Supplier\" IS NOT NULL AND LENGTH(TRIM(\"Supplier\")) > 0 ON CONFLICT (\"Name\") DO NOTHING;");
            migrationBuilder.Sql("UPDATE \"InventoryItems\" AS item SET \"SupplierId\" = supplier.\"Id\" FROM \"Suppliers\" AS supplier WHERE item.\"Supplier\" IS NOT NULL AND TRIM(item.\"Supplier\") = supplier.\"Name\";");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Suppliers_SupplierId",
                table: "InventoryItems",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Suppliers_SupplierId",
                table: "InventoryItems");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_SupplierId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "InventoryItems");
        }
    }
}
