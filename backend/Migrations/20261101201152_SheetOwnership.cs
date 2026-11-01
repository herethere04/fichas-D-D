using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDSheetApi.Migrations
{
    /// <inheritdoc />
    public partial class SheetOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerId",
                table: "CharacterSheets",
                type: "integer",
                nullable: true);

            // Assign existing sheets only to the pre-existing admin; fail safely if absent.
            migrationBuilder.Sql("UPDATE \"CharacterSheets\" SET \"OwnerId\" = (SELECT \"Id\" FROM \"Users\" WHERE \"Username\" = 'admin')");
            migrationBuilder.AlterColumn<int>(name: "OwnerId", table: "CharacterSheets", type: "integer", nullable: false, oldClrType: typeof(int), oldType: "integer", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_OwnerId",
                table: "CharacterSheets",
                column: "OwnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSheets_Users_OwnerId",
                table: "CharacterSheets",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_Users_OwnerId",
                table: "CharacterSheets");

            migrationBuilder.DropIndex(
                name: "IX_CharacterSheets_OwnerId",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "CharacterSheets");
        }
    }
}
