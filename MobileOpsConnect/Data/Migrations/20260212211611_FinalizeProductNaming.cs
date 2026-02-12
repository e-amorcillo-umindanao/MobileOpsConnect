using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileOpsConnect.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalizeProductNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Product",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ReorderPoint",
                table: "Product");

            migrationBuilder.RenameTable(
                name: "Product",
                newName: "Products");

            migrationBuilder.RenameColumn(
                name: "UnitPrice",
                table: "Products",
                newName: "Price");

            migrationBuilder.RenameColumn(
                name: "StockLevel",
                table: "Products",
                newName: "StockQuantity");

            migrationBuilder.RenameColumn(
                name: "ProductName",
                table: "Products",
                newName: "SKU");

            migrationBuilder.RenameColumn(
                name: "Barcode",
                table: "Products",
                newName: "Name");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Products",
                table: "Products",
                column: "ProductID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Products",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Products");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Product");

            migrationBuilder.RenameColumn(
                name: "StockQuantity",
                table: "Product",
                newName: "StockLevel");

            migrationBuilder.RenameColumn(
                name: "SKU",
                table: "Product",
                newName: "ProductName");

            migrationBuilder.RenameColumn(
                name: "Price",
                table: "Product",
                newName: "UnitPrice");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Product",
                newName: "Barcode");

            migrationBuilder.AddColumn<int>(
                name: "ReorderPoint",
                table: "Product",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Product",
                table: "Product",
                column: "ProductID");
        }
    }
}
