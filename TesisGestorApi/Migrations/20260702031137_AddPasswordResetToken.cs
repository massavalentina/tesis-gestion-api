using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TesisGestorApi.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""PasswordResetTokens"" (
                    ""Id"" uuid NOT NULL,
                    ""Token"" text NOT NULL,
                    ""IdUsuario"" uuid NOT NULL,
                    ""FechaCreacion"" timestamp with time zone NOT NULL,
                    ""Expiracion"" timestamp with time zone NOT NULL,
                    ""Usado"" boolean NOT NULL,
                    CONSTRAINT ""PK_PasswordResetTokens"" PRIMARY KEY (""Id"")
                );

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'PK_PasswordResetTokens'
                          AND conrelid = '""PasswordResetTokens""'::regclass
                    ) THEN
                        ALTER TABLE ""PasswordResetTokens""
                        ADD CONSTRAINT ""PK_PasswordResetTokens"" PRIMARY KEY (""Id"");
                    END IF;
                END $$;

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_constraint
                        WHERE conname = 'FK_PasswordResetTokens_Usuarios_IdUsuario'
                          AND conrelid = '""PasswordResetTokens""'::regclass
                    ) THEN
                        ALTER TABLE ""PasswordResetTokens""
                        ADD CONSTRAINT ""FK_PasswordResetTokens_Usuarios_IdUsuario""
                        FOREIGN KEY (""IdUsuario"") REFERENCES ""Usuarios"" (""IdUsuario"") ON DELETE CASCADE;
                    END IF;
                END $$;

                CREATE INDEX IF NOT EXISTS ""IX_PasswordResetTokens_IdUsuario""
                    ON ""PasswordResetTokens"" (""IdUsuario"");

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PasswordResetTokens_Token""
                    ON ""PasswordResetTokens"" (""Token"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetTokens");
        }
    }
}
