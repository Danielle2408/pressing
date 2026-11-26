using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pressing.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerOnlineRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectionAddress",
                table: "LaundryOrders",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectionRequestedAtUtc",
                table: "LaundryOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "LaundryOrders",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnlineRequest",
                table: "LaundryOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectionAddress",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "CollectionRequestedAtUtc",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "DeliveryAddress",
                table: "LaundryOrders");

            migrationBuilder.DropColumn(
                name: "IsOnlineRequest",
                table: "LaundryOrders");
        }
    }
}
