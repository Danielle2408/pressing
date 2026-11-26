using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pressing.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnsureSingleDefaultCustomerAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CustomerAddresses_CustomerId",
                table: "CustomerAddresses",
                column: "CustomerId",
                unique: true,
                filter: "\"IsActive\" AND \"IsDefault\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerAddresses_CustomerId",
                table: "CustomerAddresses");
        }
    }
}
