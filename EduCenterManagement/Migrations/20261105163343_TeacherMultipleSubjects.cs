using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class TeacherMultipleSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "teacher_subjects",
                columns: table => new
                {
                    TeacherId = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teacher_subjects", x => new { x.TeacherId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_teacher_subjects_subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_teacher_subjects_teacher_profiles_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "teacher_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_teacher_subjects_SubjectId",
                table: "teacher_subjects",
                column: "SubjectId");

            // Preserve every existing assignment, including inactive subjects.
            migrationBuilder.Sql("""
                INSERT INTO [teacher_subjects] ([TeacherId], [SubjectId])
                SELECT [Id], [SubjectId] FROM [teacher_profiles];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles");

            migrationBuilder.DropIndex(
                name: "IX_teacher_profiles_SubjectId",
                table: "teacher_profiles");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "teacher_profiles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A single SubjectId cannot represent multiple assignments. Refuse
            // rollback instead of silently discarding a teacher's subjects.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT t.[Id]
                    FROM [teacher_profiles] t
                    LEFT JOIN [teacher_subjects] ts ON ts.[TeacherId] = t.[Id]
                    GROUP BY t.[Id]
                    HAVING COUNT(ts.[SubjectId]) <> 1
                )
                BEGIN
                    ;THROW 51000, 'Cannot roll back: each teacher must have exactly one subject.', 1;
                END;
                """);

            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "teacher_profiles",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE t SET t.[SubjectId] = ts.[SubjectId]
                FROM [teacher_profiles] t
                INNER JOIN [teacher_subjects] ts ON ts.[TeacherId] = t.[Id];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "SubjectId",
                table: "teacher_profiles",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_teacher_profiles_SubjectId",
                table: "teacher_profiles",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_teacher_profiles_subjects_SubjectId",
                table: "teacher_profiles",
                column: "SubjectId",
                principalTable: "subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropTable(
                name: "teacher_subjects");
        }
    }
}
