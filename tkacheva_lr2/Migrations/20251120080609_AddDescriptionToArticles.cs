using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tkacheva_lr2.Migrations
{
    /// <inheritdoc />
    public partial class AddDescriptionToArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Articles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Articles",
                keyColumn: "Id",
                keyValue: 1,
                column: "Description",
                value: null);

            migrationBuilder.UpdateData(
                table: "Articles",
                keyColumn: "Id",
                keyValue: 2,
                column: "Description",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Articles");
        }
    }
}
