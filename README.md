# LINDE.Api

Backend de LINDE: ASP.NET Core Web API en .NET 10, con los patrones de diseño definidos en los documentos de patrones creacionales, estructurales y de comportamiento.

## Cómo ejecutarlo

Requisitos: SDK de .NET 10, PostgreSQL 19 con PostGIS 3.7 y la herramienta `dotnet-ef` (`dotnet tool install --global dotnet-ef`).

### Primera vez: base de datos

1. Crea la base y activa PostGIS (en pgAdmin, Query Tool sobre la base `linde`, o en SQL Shell):
   ```sql
   CREATE DATABASE linde;
   \c linde
   CREATE EXTENSION postgis;
   ```
2. Guarda la cadena de conexión con tu contraseña. Va en `dotnet user-secrets`, fuera del repositorio; cada integrante pone la suya:
   ```
   cd backend/LINDE.Api
   dotnet user-secrets set "ConnectionStrings:Linde" "Host=localhost;Port=5432;Database=linde;Username=postgres;Password=TU_CONTRASEÑA"
   ```
3. Crea las tablas:
   ```
   dotnet ef database update
   ```
   En desarrollo no es obligatorio: la API aplica las migraciones pendientes al arrancar.

`appsettings.json` solo tiene la cadena sin contraseña. Para trabajar sin PostgreSQL (datos en memoria), cambia `"Datos": { "Proveedor": "Memoria" }`.

### Ejecutar

```
cd backend
dotnet run --project LINDE.Api
```

La API queda en http://localhost:5080. En Rider, abre `backend/LINDE.sln` y ejecuta el perfil `http`.
Para probarla, abre `LINDE.Api/LINDE.Api.http` y ejecuta las peticiones en orden: las primeras registran un productor e inician sesión con el administrador, y el token se usa en las demás.

Pruebas automáticas (una o más por patrón, y las del sprint 2):

```
dotnet test
```

## Autenticación y roles (CU-01, RF-11, RNF-03)

- `POST /api/auth/registro` (registro de productor) y `POST /api/auth/login` son públicos. El login devuelve un token JWT con el id, el nombre, el correo, el rol y, si es productor, su id de productor.
- Todos los demás endpoints exigen el encabezado `Authorization: Bearer <token>`; sin él responden 401.
- Las contraseñas se guardan con hash (`PasswordHasher` de ASP.NET Core), nunca en texto plano.
- La clave que firma los tokens está en `appsettings.Development.json` (`Jwt:Clave`). Es solo para desarrollo: en producción se configura fuera del repositorio.

Cuentas cargadas previamente (sección `CuentasIniciales` de `appsettings.Development.json`, se crean al arrancar):

| Rol | Correo | Contraseña |
|---|---|---|
| Administrador | admin@linde.bo | Admin-2026 |

Es solo para desarrollo. Un administrador puede crear otros administradores con `POST /api/auth/administradores`.

LINDE tiene solo dos roles: Productor y Administrador. El técnico verificador y el SENASAG no tienen cuenta: el productor les muestra el expediente (en pantalla, impreso o en PDF) y ellos pueden comprobarlo por su código en la consulta pública.

Permisos por rol:

| Operación | Roles |
|---|---|
| Registrarse como productor | Público |
| Ver, registrar, editar o eliminar productores | Administrador |
| Registrar, editar o eliminar predios y polígonos, y enviar a revisión | Productor (solo los suyos), Administrador |
| Ver predios, polígonos, validaciones y alertas | Productor (solo los suyos), Administrador |
| Generar y ver expedientes (conformes o con alertas) | Productor (solo los suyos), Administrador |
| Consultar un expediente por su código (`GET /api/consulta/{codigo}`, CI enmascarado) | Público |
| Modificar parámetros, ver la bitácora, crear administradores | Administrador |

Un productor que intenta abrir el predio de otro recibe 403 "Acceso denegado" (`Autenticacion/ControlAcceso.cs`).

## Sprint 2

- **RF-01 Registro de productor** (`Servicios/ServicioProductores.cs`): nombre, CI o NIT, RUNPA opcional, teléfono, municipio y cuenta de acceso. Se asigna un código único (`PRD-000001`). Un CI o NIT repetido se rechaza con "Productor ya registrado".
- **RF-02 Registro de predio** (`Servicios/ServicioPredios.cs`): nombre, municipio, cultivo, superficie declarada y número de título INRA. Sin productor o sin superficie no se guarda; la superficie acepta 2 decimales.
- **RF-03 Carga GeoJSON o KML** (`Geometria/RevisionGeometrica.cs`): la geometría se revisa al cargarla y se rechaza indicando el tipo de error (anillo no cerrado, autointersección, menos de 4 vértices, vértices duplicados o coordenadas fuera de WGS84). El lote de 20 archivos de prueba está en `LINDE.Tests/Archivos` y lo verifica `LoteArchivosTests`.
- **RF-05 Polígono obligatorio sobre 4 ha**: un punto se guarda con 6 decimales; al enviar, un predio de más de 4 ha con un solo punto se rechaza.
- **RF-06 Superposición**: al enviar, bloquea si el área superpuesta supera 100 m² o el 0,1 % y muestra el área y el predio. La búsqueda usa `ST_Intersects` con índice espacial en PostGIS.

## Base de datos (PostgreSQL + PostGIS)

- `Datos/LindeDbContext.cs`: contexto de EF Core y el mapeo de cada tabla con Fluent API (claves foráneas, índices únicos de CI, correo y código, restricciones CHECK de superficie).
- `Datos/RepositorioEf.cs`: implementación de `IRepositorio<T>` con EF Core. Los servicios no cambiaron: reciben la interfaz.
- `Datos/RepositorioMemoria` (en `Repositorios.cs`): la versión en listas, que usan las pruebas automáticas.
- `Datos/Migraciones/`: migración `Inicial` (10 tablas). Para un cambio en el modelo: `dotnet ef migrations add NombreDelCambio --output-dir Datos/Migraciones`.
- **Polígonos:** columna `Geometria` de tipo `geometry(Geometry, 4326)` (WGS84) con índice espacial GiST. En C# el polígono sigue siendo una lista de vértices; la propiedad `Poligono.Geometria` la convierte al guardar y al leer.
- **Superposición (RF-06):** la capa geométrica pide a la base solo los polígonos que tocan al del predio; EF Core lo traduce a `ST_Intersects` y usa el índice. El área superpuesta se calcula en metros con la proyección de área equivalente (RNF-09).
- Resultados de validación, expedientes y parámetros guardan sus listas en columnas `jsonb`.
- La primera vez que arranca con una base vacía, el registro muestra un `fail` sobre `__EFMigrationsHistory`: es normal, EF Core comprueba si la tabla existe antes de crearla.

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
- **Superposición entre productores:** se compara contra los predios de todos los productores, pero el aviso solo muestra el nombre del otro predio si es del mismo productor; si es de otro, dice "de otro productor".
- **Departamentos y municipios:** `Dominio/DivisionPolitica.cs` tiene los 9 departamentos y los 340 municipios de Bolivia. Al registrar un predio, el municipio debe ser del departamento elegido (no importan las mayúsculas ni las tildes). Las pruebas están en `LINDE.Tests/DivisionPoliticaTests.cs`.
- **Áreas:** se calculan en metros con una proyección de área equivalente centrada en el predio, no en grados (RNF-09).
- **Usuario de la bitácora:** se toma del token (el correo de la sesión).

## Cambio de modelo: sin roles de verificador ni SENASAG

- **Estados del predio** (patrón State, `Dominio/Estados/EstadosPredio.cs`): Registrado, Con alertas y Aprobado. Al enviar, si alguna capa genera alertas el predio queda "Con alertas"; solo queda "Aprobado" si las cuatro capas están conformes. Nadie lo aprueba a mano: un predio con alertas se corrige y se vuelve a enviar.
- **Expediente** (`Expedientes/Expedientes.cs`): se genera para predios aprobados y para predios con alertas. Indica su estado (Conforme o Con alertas), lista las alertas pendientes y lleva la leyenda de que LINDE no certifica.
- **Consulta pública** (`GET /api/consulta/{codigo}`): sin token y de solo lectura, con el CI del productor enmascarado.
- **Alertas** (`GET /api/alertas`): el productor ve las de sus predios y el administrador, todas, ordenadas por prioridad.
- **Migración `SinRolesExternos`**: borra las cuentas de verificador y SENASAG, pasa los predios "En revisión" u "Observado" a "Con alertas" y agrega el estado y las alertas al expediente. Se aplica sola al arrancar en Development.
