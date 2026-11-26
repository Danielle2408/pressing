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
            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_CustomerAddresses_CustomerId\" ON \"CustomerAddresses\" (\"CustomerId\") WHERE \"IsActive\" AND \"IsDefault\";");
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
