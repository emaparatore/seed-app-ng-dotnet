using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Seed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceRequestServiceReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceName",
                table: "InvoiceRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServicePeriodEnd",
                table: "InvoiceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ServicePeriodStart",
                table: "InvoiceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserSubscriptionId",
                table: "InvoiceRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_UserSubscriptionId",
                table: "InvoiceRequests",
                column: "UserSubscriptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceRequests_UserSubscriptions_UserSubscriptionId",
                table: "InvoiceRequests",
                column: "UserSubscriptionId",
                principalTable: "UserSubscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceRequests_UserSubscriptions_UserSubscriptionId",
                table: "InvoiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRequests_UserSubscriptionId",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "ServiceName",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "ServicePeriodEnd",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "ServicePeriodStart",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "UserSubscriptionId",
                table: "InvoiceRequests");
        }
    }
}
