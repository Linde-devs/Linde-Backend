namespace LINDE.Api.Contratos;

/// <summary>RF-01. Registro de un productor con su cuenta de acceso (registro público o hecho por un administrador).</summary>
public record RegistroProductorDto(string Nombre, string Apellido, string Ci, string? Runpa, string Telefono, string Municipio,
    string Correo, string Contrasena);

/// <summary>Edición de los datos del productor. La cuenta de acceso no se cambia aquí.</summary>
public record ProductorDto(string Nombre, string Apellido, string Ci, string? Runpa, string Telefono, string Municipio);

/// <summary>Lo que el frontend ve de un productor.</summary>
public record ProductorVista(int Id, string Codigo, string Nombre, string Apellido, string Ci, string? Runpa, string Telefono,
    string Municipio, string Correo, DateTime FechaRegistro, int CantidadPredios);
