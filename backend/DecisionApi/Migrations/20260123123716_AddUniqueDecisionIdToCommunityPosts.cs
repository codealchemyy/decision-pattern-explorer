using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DecisionApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueDecisionIdToCommunityPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunityPosts_DecisionId",
                table: "CommunityPosts");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_DecisionId",
                table: "CommunityPosts",
                column: "DecisionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CommunityPosts_DecisionId",
                table: "CommunityPosts");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPosts_DecisionId",
                table: "CommunityPosts",
                column: "DecisionId");
        }
    }
}
