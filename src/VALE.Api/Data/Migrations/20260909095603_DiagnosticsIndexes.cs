using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VALE.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiagnosticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RequestFailures_CompanyId_OccurredAt",
                table: "RequestFailures",
                columns: new[] { "CompanyId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestFailures_OccurredAt",
                table: "RequestFailures",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_RequestFailures_TraceId",
                table: "RequestFailures",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RequestFailures_CompanyId_OccurredAt",
                table: "RequestFailures");

            migrationBuilder.DropIndex(
                name: "IX_RequestFailures_OccurredAt",
                table: "RequestFailures");

            migrationBuilder.DropIndex(
                name: "IX_RequestFailures_TraceId",
                table: "RequestFailures");
        }
    }
}
