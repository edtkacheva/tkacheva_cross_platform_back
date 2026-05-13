using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tkacheva_lr2.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleKeywordsp2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedText",
                table: "ArticleKeywords",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleKeywords_ArticleId_Source",
                table: "ArticleKeywords",
                columns: new[] { "ArticleId", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArticleKeywords_ArticleId_Source",
                table: "ArticleKeywords");

            migrationBuilder.DropColumn(
                name: "NormalizedText",
                table: "ArticleKeywords");
        }
    }
}
