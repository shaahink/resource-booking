using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class ResourceSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Resources",
                type: "character varying(140)",
                maxLength: 140,
                nullable: false,
                defaultValue: "");

            // Rows that already exist get a slug from their name (the same rule as Slugs.From),
            // numbered when two names collapse to one, before the unique index goes on.
            migrationBuilder.Sql("""
                UPDATE "Resources" AS r SET "Slug" = s.slug
                FROM (
                    SELECT "Id", base || CASE WHEN rn > 1 THEN '-' || rn ELSE '' END AS slug
                    FROM (
                        SELECT "Id", base, row_number() OVER (PARTITION BY base ORDER BY "CreatedOn", "Id") AS rn
                        FROM (
                            SELECT "Id", "CreatedOn",
                                   COALESCE(NULLIF(trim(both '-' from regexp_replace(lower("Name"), '[^a-z0-9]+', '-', 'g')), ''), 'resource') AS base
                            FROM "Resources"
                        ) AS named
                    ) AS numbered
                ) AS s
                WHERE r."Id" = s."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_Slug",
                table: "Resources",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resources_Slug",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Resources");
        }
    }
}
