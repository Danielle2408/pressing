using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pressing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotchPayCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReceiptNumber",
                table: "OrderPayments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "CheckoutUrl",
                table: "OrderPayments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MerchantReference",
                table: "OrderPayments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.Sql("UPDATE \"OrderPayments\" SET \"MerchantReference\" = 'legacy-' || \"Id\"::text WHERE \"MerchantReference\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "MerchantReference",
                table: "OrderPayments",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderPayments_MerchantReference",
                table: "OrderPayments",
                column: "MerchantReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderPayments_MerchantReference",
                table: "OrderPayments");

            migrationBuilder.DropColumn(
                name: "CheckoutUrl",
                table: "OrderPayments");

            migrationBuilder.DropColumn(
                name: "MerchantReference",
                table: "OrderPayments");

            migrationBuilder.AlterColumn<string>(
                name: "ReceiptNumber",
                table: "OrderPayments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
