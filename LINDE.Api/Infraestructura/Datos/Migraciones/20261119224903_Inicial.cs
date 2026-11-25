using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LINDE.Api.Datos.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Bitacora",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Usuario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Operacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Entidad = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntidadId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ValoresAnteriores = table.Column<string>(type: "text", nullable: true),
                    Resultado = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bitacora", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parametros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Datos = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ci = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Correo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ContrasenaHash = table.Column<string>(type: "text", nullable: false),
                    Rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProductorId = table.Column<int>(type: "integer", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Productores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Ci = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Runpa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    Municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Productores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Productores_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Predios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductorId = table.Column<int>(type: "integer", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Cultivo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    SuperficieDeclaradaHa = table.Column<double>(type: "double precision", nullable: false),
                    NumeroTituloInra = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DesmonteDeclaradoHa = table.Column<double>(type: "double precision", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ComentarioVerificador = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predios", x => x.Id);
                    table.CheckConstraint("CK_Predios_Desmonte", "\"DesmonteDeclaradoHa\" IS NULL OR \"DesmonteDeclaradoHa\" >= 0");
                    table.CheckConstraint("CK_Predios_Superficie", "\"SuperficieDeclaradaHa\" > 0");
                    table.ForeignKey(
                        name: "FK_Predios_Productores_ProductorId",
                        column: x => x.ProductorId,
                        principalTable: "Productores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Alertas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredioId = table.Column<int>(type: "integer", nullable: false),
                    Capa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Prioridad = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Resuelta = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Alertas_Predios_PredioId",
                        column: x => x.PredioId,
                        principalTable: "Predios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredioId = table.Column<int>(type: "integer", nullable: false),
                    Tipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Numero = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    TitularCi = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    SuperficieHa = table.Column<double>(type: "double precision", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentos_Predios_PredioId",
                        column: x => x.PredioId,
                        principalTable: "Predios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expedientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Productor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CiProductor = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    PredioId = table.Column<int>(type: "integer", nullable: false),
                    Predio = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Municipio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SuperficieDeclaradaHa = table.Column<double>(type: "double precision", nullable: false),
                    AreaPoligonoHa = table.Column<double>(type: "double precision", nullable: false),
                    VersionPoligono = table.Column<int>(type: "integer", nullable: false),
                    Vertices = table.Column<string>(type: "jsonb", nullable: false),
                    Validaciones = table.Column<string>(type: "jsonb", nullable: false),
                    Documentos = table.Column<string>(type: "jsonb", nullable: false),
                    ResolucionVerificador = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expedientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expedientes_Predios_PredioId",
                        column: x => x.PredioId,
                        principalTable: "Predios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Poligonos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredioId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Autor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Vigente = table.Column<bool>(type: "boolean", nullable: false),
                    Geometria = table.Column<Geometry>(type: "geometry(Geometry, 4326)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Poligonos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Poligonos_Predios_PredioId",
                        column: x => x.PredioId,
                        principalTable: "Predios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultadosValidacion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PredioId = table.Column<int>(type: "integer", nullable: false),
                    VersionPoligono = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Capas = table.Column<string>(type: "jsonb", nullable: false),
                    Cortado = table.Column<bool>(type: "boolean", nullable: false),
                    EstadoResultante = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Mensaje = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultadosValidacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultadosValidacion_Predios_PredioId",
                        column: x => x.PredioId,
                        principalTable: "Predios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Alertas_PredioId_Resuelta",
                table: "Alertas",
                columns: new[] { "PredioId", "Resuelta" });

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Entidad_EntidadId",
                table: "Bitacora",
                columns: new[] { "Entidad", "EntidadId" });

            migrationBuilder.CreateIndex(
                name: "IX_Bitacora_Fecha",
                table: "Bitacora",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_PredioId",
                table: "Documentos",
                column: "PredioId");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_Codigo",
                table: "Expedientes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_PredioId",
                table: "Expedientes",
                column: "PredioId");

            migrationBuilder.CreateIndex(
                name: "IX_Poligonos_Geometria",
                table: "Poligonos",
                column: "Geometria")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_Poligonos_PredioId_Version",
                table: "Poligonos",
                columns: new[] { "PredioId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Poligonos_PredioId_Vigente",
                table: "Poligonos",
                columns: new[] { "PredioId", "Vigente" });

            migrationBuilder.CreateIndex(
                name: "IX_Predios_ProductorId",
                table: "Predios",
                column: "ProductorId");

            migrationBuilder.CreateIndex(
                name: "IX_Productores_Ci",
                table: "Productores",
                column: "Ci",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Productores_Codigo",
                table: "Productores",
                column: "Codigo",
                unique: true,
                filter: "\"Codigo\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Productores_UsuarioId",
                table: "Productores",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultadosValidacion_PredioId",
                table: "ResultadosValidacion",
                column: "PredioId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_ProductorId",
                table: "Usuarios",
                column: "ProductorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alertas");

            migrationBuilder.DropTable(
                name: "Bitacora");

            migrationBuilder.DropTable(
                name: "Documentos");

            migrationBuilder.DropTable(
                name: "Expedientes");

            migrationBuilder.DropTable(
                name: "Parametros");

            migrationBuilder.DropTable(
                name: "Poligonos");

            migrationBuilder.DropTable(
                name: "ResultadosValidacion");

            migrationBuilder.DropTable(
                name: "Predios");

            migrationBuilder.DropTable(
                name: "Productores");

            migrationBuilder.DropTable(
                name: "Usuarios");
        }
    }
}
