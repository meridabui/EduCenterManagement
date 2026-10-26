using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherSubjectName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_SubjectId",
                table: "teacher_profiles",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles");

            migrationBuilder.DropIndex(
                name: "IX_teacher_profiles_SubjectId",
                table: "teacher_profiles");
        }
    }
}
