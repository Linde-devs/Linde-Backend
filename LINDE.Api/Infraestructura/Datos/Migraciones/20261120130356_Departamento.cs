using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LINDE.Api.Datos.Migraciones
{
    /// <summary>Agrega el departamento al predio y al expediente. Los predios existentes quedan en Santa Cruz.</summary>
    public partial class Departamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Departamento",
                table: "Predios",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Santa Cruz");

            migrationBuilder.AddColumn<string>(
                name: "Departamento",
                table: "Expedientes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            // Hasta ahora todos los predios eran de Santa Cruz: los expedientes ya generados toman el departamento de su predio.
            migrationBuilder.Sql(
                "UPDATE \"Expedientes\" e SET \"Departamento\" = p.\"Departamento\" FROM \"Predios\" p WHERE p.\"Id\" = e.\"PredioId\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Departamento",
                table: "Predios");

            migrationBuilder.DropColumn(
                name: "Departamento",
                table: "Expedientes");
        }
    }
}
