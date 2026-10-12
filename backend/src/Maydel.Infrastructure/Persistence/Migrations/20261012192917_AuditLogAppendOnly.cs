using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maydel.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes the audit trail append-only at the database level (E14.1): any UPDATE, DELETE or TRUNCATE on
    /// "AuditLogs" fails, whoever issues it. Production additionally revokes those privileges from the app role.
    /// </summary>
    public partial class AuditLogAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_logs_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'AuditLogs is append-only: % is not allowed', TG_OP;
                END;
                $$;

                CREATE TRIGGER audit_logs_no_update_delete
                    BEFORE UPDATE OR DELETE ON "AuditLogs"
                    FOR EACH ROW EXECUTE FUNCTION audit_logs_append_only();

                CREATE TRIGGER audit_logs_no_truncate
                    BEFORE TRUNCATE ON "AuditLogs"
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER audit_logs_no_truncate ON "AuditLogs";
                DROP TRIGGER audit_logs_no_update_delete ON "AuditLogs";
                DROP FUNCTION audit_logs_append_only();
                """);
        }
    }
}
