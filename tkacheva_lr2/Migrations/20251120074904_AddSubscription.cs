using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tkacheva_lr2.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserChannelSubscriptions",
                columns: table => new
                {
                    SubscribedChannelsId = table.Column<int>(type: "INTEGER", nullable: false),
                    SubscribersId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChannelSubscriptions", x => new { x.SubscribedChannelsId, x.SubscribersId });
                    table.ForeignKey(
                        name: "FK_UserChannelSubscriptions_AppUsers_SubscribersId",
                        column: x => x.SubscribersId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChannelSubscriptions_RSSChannels_SubscribedChannelsId",
                        column: x => x.SubscribedChannelsId,
                        principalTable: "RSSChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserChannelSubscriptions_SubscribersId",
                table: "UserChannelSubscriptions",
                column: "SubscribersId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserChannelSubscriptions");
        }
    }
}
