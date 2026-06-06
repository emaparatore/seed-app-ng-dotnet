using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Seed.Infrastructure.Persistence;

#nullable disable

namespace Seed.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260422190000_AddInvoiceRequestPeriodUniqueness")]
    public partial class AddInvoiceRequestPeriodUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "InvoiceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountSubtotal",
                table: "InvoiceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountTax",
                table: "InvoiceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountTotal",
                table: "InvoiceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingReason",
                table: "InvoiceRequests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "InvoiceRequests",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoicePeriodEnd",
                table: "InvoiceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoicePeriodStart",
                table: "InvoiceRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProrationApplied",
                table: "InvoiceRequests",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProrationAmount",
                table: "InvoiceRequests",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeInvoiceId",
                table: "InvoiceRequests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_StripePaymentIntentId",
                table: "InvoiceRequests",
                column: "StripePaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRequests_StripeInvoiceId",
                table: "InvoiceRequests",
                column: "StripeInvoiceId",
                unique: true,
                filter: "\"StripeInvoiceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceRequests_StripeInvoiceId",
                table: "InvoiceRequests");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceRequests_StripePaymentIntentId",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "AmountSubtotal",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "AmountTax",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "AmountTotal",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "BillingReason",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "InvoicePeriodEnd",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "InvoicePeriodStart",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "IsProrationApplied",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "ProrationAmount",
                table: "InvoiceRequests");

            migrationBuilder.DropColumn(
                name: "StripeInvoiceId",
                table: "InvoiceRequests");
        }
    }
}
