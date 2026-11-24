using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LINDE.Api.Datos.Migraciones
{
    /// <summary>CU-11: capas de referencia del INRA y la ABT, y sus polígonos con índice espacial para el cruce de RF-07.</summary>
    public partial class CapasReferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CapasReferencia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Fuente = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Archivo = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    CantidadElementos = table.Column<int>(type: "integer", nullable: false),
                    FechaCarga = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CargadaPor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Vigente = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapasReferencia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ElementosCapa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CapaId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    TitularCi = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    SuperficieHa = table.Column<double>(type: "double precision", nullable: true),
                    Geometria = table.Column<Geometry>(type: "geometry(Geometry, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementosCapa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ElementosCapa_CapasReferencia_CapaId",
                        column: x => x.CapaId,
                        principalTable: "CapasReferencia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapasReferencia_Tipo_Vigente",
                table: "CapasReferencia",
                columns: new[] { "Tipo", "Vigente" });

            migrationBuilder.CreateIndex(
                name: "IX_ElementosCapa_CapaId",
                table: "ElementosCapa",
                column: "CapaId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementosCapa_Geometria",
                table: "ElementosCapa",
                column: "Geometria")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_ElementosCapa_Tipo",
                table: "ElementosCapa",
                column: "Tipo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ElementosCapa");

            migrationBuilder.DropTable(
                name: "CapasReferencia");
        }
    }
}
