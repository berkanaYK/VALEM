using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VALE.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProfileHeaderAndVerifiedPhoneIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeaderBackgroundTheme",
                table: "AspNetUsers",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Profile");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true,
                filter: "\"PhoneNumberConfirmed\" = TRUE AND \"PhoneNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "HeaderBackgroundTheme",
                table: "AspNetUsers");
        }
    }
}
