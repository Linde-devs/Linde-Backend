using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LINDE.Api.Datos.Migraciones
{
    /// <summary>
    /// RF-14: tabla de inspecciones de campo y su sección en el expediente. Los expedientes ya generados quedan con la
    /// lista vacía ("[]") hasta que se vuelvan a generar.
    /// </summary>
    public partial class InspeccionesCampo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Inspecciones",
                table: "Expedientes",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "InspeccionesCampo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredioId = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Tecnico = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegistradaPor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspeccionesCampo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspeccionesCampo_Predios_PredioId",
                        column: x => x.PredioId,
                        principalTable: "Predios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InspeccionesCampo_PredioId",
                table: "InspeccionesCampo",
                column: "PredioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InspeccionesCampo");

            migrationBuilder.DropColumn(
                name: "Inspecciones",
                table: "Expedientes");
        }
    }
}
