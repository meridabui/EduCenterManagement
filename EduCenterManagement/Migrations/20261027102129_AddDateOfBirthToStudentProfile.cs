using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOfBirthToStudentProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "student_profiles",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "student_profiles");
        }
    }
}
