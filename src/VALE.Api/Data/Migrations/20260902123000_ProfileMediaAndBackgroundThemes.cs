using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VALE.Api.Data.Migrations;

[DbContext(typeof(ValeDbContext))]
[Migration("20260902123000_ProfileMediaAndBackgroundThemes")]
public sealed class ProfileMediaAndBackgroundThemes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BackgroundTheme", table: "AspNetUsers", type: "character varying(30)",
            maxLength: 30, nullable: false, defaultValue: "None");
        migrationBuilder.AddColumn<byte[]>(
            name: "ProfilePhoto", table: "AspNetUsers", type: "bytea", nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "ProfilePhotoContentType", table: "AspNetUsers", type: "character varying(30)",
            maxLength: 30, nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "BackgroundTheme", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "ProfilePhoto", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "ProfilePhotoContentType", table: "AspNetUsers");
    }
}
