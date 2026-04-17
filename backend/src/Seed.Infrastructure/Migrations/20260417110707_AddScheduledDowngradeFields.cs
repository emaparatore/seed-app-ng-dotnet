using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Seed.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledDowngradeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ScheduledPlanId",
                table: "UserSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeScheduleId",
                table: "UserSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSubscriptions_ScheduledPlanId",
                table: "UserSubscriptions",
                column: "ScheduledPlanId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_ScheduledPlanId",
                table: "UserSubscriptions",
                column: "ScheduledPlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSubscriptions_SubscriptionPlans_ScheduledPlanId",
                table: "UserSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_UserSubscriptions_ScheduledPlanId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "ScheduledPlanId",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "StripeScheduleId",
                table: "UserSubscriptions");
        }
    }
}
