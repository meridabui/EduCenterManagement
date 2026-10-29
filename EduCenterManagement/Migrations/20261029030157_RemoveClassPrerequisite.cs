using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClassPrerequisite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_classes_classes_PrerequisiteClassId",
                table: "classes");

            migrationBuilder.DropIndex(
                name: "IX_classes_PrerequisiteClassId",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "PrerequisiteClassId",
                table: "classes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrerequisiteClassId",
                table: "classes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_classes_PrerequisiteClassId",
                table: "classes",
                column: "PrerequisiteClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_classes_classes_PrerequisiteClassId",
                table: "classes",
                column: "PrerequisiteClassId",
                principalTable: "classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
