using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class MakeSessionOptionalAndAddClassToQuiz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SessionId",
                table: "quizzes",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ClassId",
                table: "quizzes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_quizzes_ClassId",
                table: "quizzes",
                column: "ClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_quizzes_classes_ClassId",
                table: "quizzes",
                column: "ClassId",
                principalTable: "classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_quizzes_classes_ClassId",
                table: "quizzes");

            migrationBuilder.DropIndex(
                name: "IX_quizzes_ClassId",
                table: "quizzes");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "quizzes");

            migrationBuilder.AlterColumn<int>(
                name: "SessionId",
                table: "quizzes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
