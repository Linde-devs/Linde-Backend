using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LINDE.Api.Datos.Migraciones
{
    /// <summary>
    /// El verificador y el SENASAG dejan de tener cuenta en LINDE: solo quedan Productor y Administrador.
    /// Los predios que estaban "En revisión" u "Observado" pasan a "Con alertas", y el expediente guarda su estado y sus alertas.
    /// </summary>
    public partial class SinRolesExternos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Usuarios\" WHERE \"Rol\" IN ('Verificador', 'Senasag');");
            migrationBuilder.Sql("UPDATE \"Predios\" SET \"Estado\" = 'ConAlertas' WHERE \"Estado\" IN ('EnRevision', 'Observado');");
            migrationBuilder.Sql("UPDATE \"ResultadosValidacion\" SET \"EstadoResultante\" = 'ConAlertas' WHERE \"EstadoResultante\" IN ('EnRevision', 'Observado');");

            migrationBuilder.DropColumn(
                name: "ComentarioVerificador",
                table: "Predios");

            migrationBuilder.DropColumn(
                name: "ResolucionVerificador",
                table: "Expedientes");

            migrationBuilder.AddColumn<string>(
                name: "Alertas",
                table: "Expedientes",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Expedientes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Conforme");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Alertas",
                table: "Expedientes");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Expedientes");

            migrationBuilder.AddColumn<string>(
                name: "ComentarioVerificador",
                table: "Predios",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolucionVerificador",
                table: "Expedientes",
                type: "text",
                nullable: true);
        }
    }
}
