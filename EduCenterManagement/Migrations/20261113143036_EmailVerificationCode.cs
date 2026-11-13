using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduCenterManagement.Migrations
{
    /// <inheritdoc />
    public partial class EmailVerificationCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_TokenHash",
                table: "email_verification_tokens");

            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_UserId",
                table: "email_verification_tokens");

            migrationBuilder.AddColumn<int>(
                name: "FailedAttempts",
                table: "email_verification_tokens",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_UserId_CreatedAt",
                table: "email_verification_tokens",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_email_verification_tokens_UserId_CreatedAt",
                table: "email_verification_tokens");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                table: "email_verification_tokens");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_TokenHash",
                table: "email_verification_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_UserId",
                table: "email_verification_tokens",
                column: "UserId");
        }
    }
}
