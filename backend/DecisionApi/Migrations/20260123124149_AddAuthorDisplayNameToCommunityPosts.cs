using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DecisionApi.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorDisplayNameToCommunityPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorDisplayName",
                table: "CommunityPosts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorDisplayName",
                table: "CommunityPosts");
        }
    }
}
