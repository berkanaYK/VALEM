using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VALE.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PremiumEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProfileFrame",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.CreateTable(
                name: "UserEntitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProductId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PurchaseTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedPurchaseToken = table.Column<string>(type: "character varying(8192)", maxLength: 8192, nullable: true),
                    OrderId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PurchasedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEntitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserEntitlements_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserEntitlements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEntitlements_CompanyId_IsActive",
                table: "UserEntitlements",
                columns: new[] { "CompanyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserEntitlements_PurchaseTokenHash",
                table: "UserEntitlements",
                column: "PurchaseTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEntitlements_UserId_Plan",
                table: "UserEntitlements",
                columns: new[] { "UserId", "Plan" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEntitlements");

            migrationBuilder.DropColumn(
                name: "ProfileFrame",
                table: "AspNetUsers");
        }
    }
}
