using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tkacheva_lr2.Migrations
{
    /// <inheritdoc />
    public partial class AddArticlesCategoies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArticleCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ArticleId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArticleCategories_Articles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "Articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCategories_ArticleId",
                table: "ArticleCategories",
                column: "ArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCategories_ArticleId_NormalizedName",
                table: "ArticleCategories",
                columns: new[] { "ArticleId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArticleCategories_NormalizedName",
                table: "ArticleCategories",
                column: "NormalizedName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArticleCategories");
        }
    }
}
