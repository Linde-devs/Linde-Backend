using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LINDE.Api.Datos.Migraciones
{
    /// <summary>RF-16: datos del archivo PDF o imagen de cada documento. El archivo se guarda fuera de la base.</summary>
    public partial class ArchivosDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ArchivoBytes",
                table: "Documentos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivoClave",
                table: "Documentos",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivoFecha",
                table: "Documentos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivoNombre",
                table: "Documentos",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivoTipo",
                table: "Documentos",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivoBytes",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "ArchivoClave",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "ArchivoFecha",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "ArchivoNombre",
                table: "Documentos");

            migrationBuilder.DropColumn(
                name: "ArchivoTipo",
                table: "Documentos");
        }
    }
}
