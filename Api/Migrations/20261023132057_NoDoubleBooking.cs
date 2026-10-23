using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// The database-level backstop against double booking: a GiST exclusion constraint on
    /// confirmed bookings of exclusive (capacity-1) resources. The service layer checks first
    /// and returns a friendly 409; this constraint guarantees that even a bug cannot
    /// double-book — two overlapping confirmed rows for the same exclusive resource are
    /// impossible to commit. Pooled resources (capacity > 1) are enforced in the service
    /// inside a transaction holding a per-resource advisory lock.
    /// </summary>
    public partial class NoDoubleBooking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS btree_gist;""");

            migrationBuilder.Sql("""
                ALTER TABLE "Bookings" ADD CONSTRAINT no_double_booking
                EXCLUDE USING gist (
                    "ResourceId" WITH =,
                    tstzrange("Start", "End") WITH &&
                )
                WHERE ("Status" = 'Confirmed' AND "IsExclusive");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE "Bookings" DROP CONSTRAINT no_double_booking;""");
        }
    }
}
