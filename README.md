# LINDE.Api

Backend de LINDE: ASP.NET Core Web API en .NET 10, con los patrones de diseño definidos en los documentos de patrones creacionales, estructurales y de comportamiento.

## Cómo ejecutarlo

Requisito: SDK de .NET 10.

```
cd backend
dotnet run --project LINDE.Api
```

La API queda en http://localhost:5080. En Rider, abre `backend/LINDE.sln` y ejecuta el perfil `http`.
Para probarla, abre `LINDE.Api/LINDE.Api.http` y ejecuta las peticiones en orden: las primeras crean la cuenta e inician sesión, y el token se usa en las demás.

Pruebas automáticas (una o más por patrón):

```
dotnet test
```

## Autenticación (CU-01)

- `POST /api/auth/registro` y `POST /api/auth/login` son públicos. El login devuelve un token JWT con el id, el nombre, el correo y el rol.
- Todos los demás endpoints exigen el encabezado `Authorization: Bearer <token>`; sin él responden 401.
- Roles: aprobar y observar predios solo para Verificador o Administrador; modificar parámetros y ver la bitácora solo para Administrador.
- Las contraseñas se guardan con hash (`PasswordHasher` de ASP.NET Core), nunca en texto plano.
- La clave que firma los tokens está en `appsettings.Development.json` (`Jwt:Clave`). Es solo para desarrollo: en producción se configura fuera del repositorio.
- Por ahora el registro crea administradores. Más adelante el registro público será solo para productores.

## Almacenamiento temporal

Los datos (incluidos los usuarios) se guardan en listas en memoria (`Datos/Repositorios.cs`) y se borran al reiniciar la API.
Cuando exista PostgreSQL + PostGIS, se reemplaza `RepositorioMemoria` por una implementación con EF Core sin tocar los servicios.

## Dónde está cada patrón

| Tipo | Patrón | Archivo | Caso de uso |
|---|---|---|---|
| Creacional | Factory Method (lectores GeoJSON y KML) | `Importacion/LectoresPoligono.cs` | CU-04 |
| Creacional | Factory Method por contenedor (validadores) | `Infraestructura/RegistroServicios.cs` | CU-07 |
| Creacional | Builder (expediente) | `Expedientes/Expedientes.cs` | CU-10 |
| Creacional | Prototype (versiones del polígono) | `Dominio/Poligono.cs`, `Servicios/ServicioPoligonos.cs` | CU-04 |
| Creacional | Singleton por contenedor (parámetros) | `Configuracion/ServicioParametros.cs` | CU-12 |
| Estructural | Adapter (Google Earth Engine) | `Cobertura/ProveedoresCobertura.cs` | CU-07 |
| Estructural | Facade (validación de predios) | `Validacion/ServicioValidacionPredios.cs` | CU-06, CU-07 |
| Estructural | Decorator (bitácora de auditoría) | `Servicios/*Auditado`, `Configuracion/ServicioParametros.cs` | CU-03, 04, 05, 12 |
| Estructural | Proxy (caché del análisis satelital) | `Cobertura/ProveedoresCobertura.cs` | CU-07 |
| Comportamiento | Chain of Responsibility (cuatro capas) | `Validacion/CadenaValidacion.cs`, `Validacion/Validadores.cs` | CU-07 |
| Comportamiento | State (ciclo de vida del predio) | `Dominio/Estados/EstadosPredio.cs`, `Dominio/Predio.cs` | CU-03, 06, 09, 10 |
| Comportamiento | Strategy (Ley 741) | `Validacion/UsoSuelo/EstrategiasUsoSuelo.cs` | CU-07 |

Abstract Factory no se implementa: el documento de patrones creacionales lo deja diferido hasta que exista la integración con INRA y ABT.

## Notas

- **Earth Engine:** por defecto se usa `CoberturaSimulada` (`"Cobertura:Proveedor": "Simulado"` en `appsettings.json`). Para usar el adaptador real, cambia el proveedor a `"EarthEngine"` y configura `UrlServicio` y `Token`.
- **Superposición:** el ERS (RF-06) dice que bloquea el envío. Como esa decisión está pendiente, es el parámetro `superposicionBloquea` y se cambia desde `PUT /api/parametros`.
- **Áreas:** se calculan en metros con una proyección de área equivalente centrada en el predio, no en grados (RNF-09).
- **Usuario de la bitácora:** se toma del token (el correo de la sesión).
