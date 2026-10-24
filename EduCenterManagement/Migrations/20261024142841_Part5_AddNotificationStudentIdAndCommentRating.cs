using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class Part5_AddNotificationStudentIdAndCommentRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Rating",
                table: "teacher_comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StudentId",
                table: "notifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_StudentId",
                table: "notifications",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_student_profiles_StudentId",
                table: "notifications",
                column: "StudentId",
                principalTable: "student_profiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_student_profiles_StudentId",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_StudentId",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "Rating",
                table: "teacher_comments");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "notifications");
        }
    }
}
