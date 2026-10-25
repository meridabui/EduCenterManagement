using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class RequireSubjectForTeacher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles");

            migrationBuilder.AlterColumn<int>(
                name: "SubjectId",
                table: "teacher_profiles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles");

            migrationBuilder.AlterColumn<int>(
                name: "SubjectId",
                table: "teacher_profiles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id");
        }
    }
}
