using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileOpsConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeStatusConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "LeaveRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseOrders_Status",
                table: "PurchaseOrders",
                sql: "[Status] IN ('Pending','Approved','Rejected')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LeaveRequests_Status",
                table: "LeaveRequests",
                sql: "[Status] IN ('Pending','Approved','Rejected')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseOrders_Status",
                table: "PurchaseOrders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LeaveRequests_Status",
                table: "LeaveRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "LeaveRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
