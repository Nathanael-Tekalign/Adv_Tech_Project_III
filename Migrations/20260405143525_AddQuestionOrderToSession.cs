using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TriviaGame.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionOrderToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestionOrderJson",
                table: "GameSessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestionOrderJson",
                table: "GameSessions");
        }
    }
}
