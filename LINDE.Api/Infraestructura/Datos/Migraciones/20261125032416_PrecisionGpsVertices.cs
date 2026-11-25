using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LINDE.Api.Datos.Migraciones
{
    /// <summary>
    /// RF-04: precisión del GPS por vértice. Los polígonos que ya existían quedan con la lista vacía ("[]"), igual que
    /// los que se cargan desde un archivo.
    /// </summary>
    public partial class PrecisionGpsVertices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrecisionesM",
                table: "Poligonos",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecisionesM",
                table: "Poligonos");
        }
    }
}
