using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddRolSecretario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO public."Roles" ("IdRol", "Nombre")
                VALUES ('55555555-5555-5555-5555-555555555555', 'Secretario')
                ON CONFLICT DO NOTHING;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM public."Roles"
                WHERE "IdRol" = '55555555-5555-5555-5555-555555555555';
            """);
        }
    }
}
