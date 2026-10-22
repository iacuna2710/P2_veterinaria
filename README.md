# Práctica 2 — Veterinaria

ASP.NET Core MVC y API con controladores en .NET 10. La web consume la API por HTTP; únicamente la API accede a SQL Server mediante Dapper y procedimientos almacenados.

## Requisitos y base de datos

- SDK .NET 10, SQL Server y la base existente `Practica2` con las tablas del enunciado.
- La configuración local usa `localhost` y autenticación integrada de Windows. El usuario que inicia la API necesita consultar e insertar en ambas tablas y ejecutar los procedimientos de `dbo`.
- Los procedimientos ya se aplicaron en este equipo. Para instalarlos o actualizarlos en otro entorno, desde esta carpeta:

```powershell
sqlcmd -S localhost -E -d Practica2 -i database/Procedimientos.sql -b
```

El script utiliza `CREATE OR ALTER`; no crea la base, cambia tablas ni borra datos. No volver a ejecutar el script original que crea la base.

La conexión se encuentra en `API_veterinaria/API_veterinaria/appsettings.json`, clave `ConnectionStrings:Practica2`. Se puede sobrescribir con la variable `ConnectionStrings__Practica2`. `TrustServerCertificate=True` corresponde al SQL Server local; en un despliegue se debe usar un certificado válido y desactivar esa excepción. No guardar contraseñas en el repositorio.

## Iniciar

Conservar las dos soluciones existentes. En dos terminales, desde la raíz:

```powershell
dotnet run --project API_veterinaria/API_veterinaria --launch-profile http
```

```powershell
dotnet run --project Web_veterinaria/Web_veterinaria --launch-profile http
```

Abrir <http://localhost:5018>. La API escucha en <http://localhost:5132>; su documento OpenAPI está en <http://localhost:5132/openapi/v1.json> durante desarrollo. También se pueden iniciar ambos proyectos desde sus soluciones de Visual Studio, usando los perfiles HTTP.

La URL de API se configura en `Api:BaseUrl` del proyecto web, o mediante `Api__BaseUrl`. Las llamadas salen del servidor MVC, por lo que no requieren CORS. En desarrollo se permite HTTP local; fuera de desarrollo se conserva la redirección a HTTPS y debe configurarse la URL HTTPS de la API.

## Funcionalidad

- **Registro de Clientes:** cédula, nombre y correo obligatorios; cédula única; `Estado` se asigna a `true`.
- **Registro de Mascotas:** nombre, especie, raza, peso y cliente obligatorios. Peso entre `0.01` y `999999.99` kg con hasta dos decimales, usando punto en los campos numéricos.
- **Consulta de Mascotas:** cédula y nombre del cliente, nombre de mascota, especie y peso.
- Máximo **dos mascotas de la misma especie en toda la veterinaria**, sin distinguir mayúsculas y recortando espacios exteriores. No se equiparan sinónimos como `Perro` y `Canino`.
- Las restricciones se validan en procedimientos dentro de transacciones con bloqueos de tabla durante el alta. Esta elección prioriza consistencia y sencillez para el volumen de la práctica sin cambiar las tablas.

Las carpetas y archivos MVC originales se reutilizan. Los controladores devuelven rutas de vista explícitas porque `ClienteController` y `MascotaController` usan carpetas de vistas plurales.

`Middleware/ErrorHandlingMiddleware.cs` controla errores inesperados de la web tanto en desarrollo como en producción: registra la excepción en el servidor y devuelve una página en español con estado HTTP `500`, referencia de seguimiento y enlace al inicio. No expone mensajes internos ni trazas, y evita almacenar la respuesta en caché. Los errores esperados de validación o conexión se muestran en el formulario conservando los datos. Si la respuesta ya comenzó, registra el fallo y lo propaga porque ya no puede reemplazarla.

## API

| Método | Ruta | Resultado |
|---|---|---|
| GET | `/api/clientes` | Clientes para el selector |
| POST | `/api/clientes` | Registra `{ cedula, nombre, correo }`; devuelve `{ idCliente }` |
| GET | `/api/mascotas` | Mascotas con `cedulaCliente` y `nombreCliente` |
| POST | `/api/mascotas` | Registra `{ nombre, especie, raza, peso, idCliente }`; devuelve `{ idMascota }` |

Respuestas: `200` para consulta, `201` para registro, `400` para validación o cliente inexistente, `409` para cédula duplicada o límite de especie, `500` para fallos inesperados. Los errores usan Problem Details y no exponen detalles internos de SQL. No hay operaciones de edición ni eliminación.

## Pruebas y prácticas ECC

Se aplican `ecc:dotnet-patterns`, `ecc:csharp-testing` y `ecc:verification-loop`: inyección de dependencias, async/await con cancelación, parámetros SQL, validación en los límites de entrada, antifalsificación y pruebas xUnit de API, vistas y persistencia.

```powershell
dotnet build Web_veterinaria/Web_veterinaria.slnx
dotnet test API_veterinaria/API_veterinaria.slnx
```

Las pruebas SQL se omiten si no se configura su conexión. Para ejecutar todas contra SQL Server local:

```powershell
$env:PRACTICA2_TEST_CONNECTION = 'Server=localhost;Database=Practica2;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test API_veterinaria/API_veterinaria.slnx --collect:"XPlat Code Coverage" --settings coverage.runsettings
```

Las pruebas SQL y la prueba web → API → SQL usan transacciones que se revierten, sin dejar filas de prueba. SQL Server puede consumir valores de identidad aun al revertir: los saltos de identificadores son normales. Las pruebas SQL se ejecutan sin paralelismo y verifican registros, restricciones, mapeo Dapper, respuestas HTTP y bloqueos entre conexiones concurrentes. Las pruebas web cubren formularios, antifalsificación, selector, datos conservados y API no disponible.

Se fija `System.Security.Cryptography.Pkcs` en `10.0.13` para resolver las alertas de la dependencia transitiva de SqlClient, según los avisos [CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx) y [CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).

## Compilar sin bloqueos de archivos

Antes de compilar, detener con Ctrl+C las dos terminales de esta práctica (o detener depuración en Visual Studio). Los errores MSB3026/MSB3027/MSB3021 aparecen si Windows mantiene abierto el ejecutable que se intenta reemplazar. No cerrar otros procesos dotnet ajenos al proyecto.

Después de compilar, iniciar una instancia de cada aplicación con los comandos anteriores. Los perfiles HTTP usan Web5018 y API5132; `Api:BaseUrl` coincide con la API5132. Si se cambian los puertos, actualizar también esa configuración.

La interfaz conserva Bootstrap local y el verde original; `site.css` centraliza colores, tipografía, foco y espaciado. Se mantienen los tres menús y la bienvenida compartida, con navegación activa, formularios accesibles y errores sin datos internos.

## Manejo centralizado de errores

La web usa `ErrorHandlingMiddleware` para excepciones y `UseStatusCodePages` para respuestas HTTP sin cuerpo. Cubre400,401/403,404,405,408/504,413,429,500 y502/503 con mensajes españoles, referencia de seguimiento, estilo compartido y `Cache-Control: no-store`. No sustituye errores de formulario ya renderizados ni respuestas que comenzaron a enviarse, y conserva encabezados como `Allow`. HEAD no genera cuerpo.

La API usa el middleware `UseExceptionHandler` con `ApiExceptionHandler` y páginas de estado en formato `application/problem+json`. La cédula repetida devuelve409 como resultado esperado, sin excepciónSQL; el límite de especie devuelve409 como resultado esperado, sin excepciónSQL;50003/50004 devuelven400; los tiempos de espera devuelven504 y los errores conocidos de conexión o deadlock503. Los demás errores internos conservan500. Las respuestas incluyen `traceId`; los detalles técnicos se registran solo en el servidor. No se reintentan inserciones automáticamente.

La cancelación del navegador no se registra como un fallo interno: se utiliza499 cuando aún es posible marcar el estado. Un cliente desconectado no recibirá una página. Las listas JSON nulas o malformadas de la API se tratan como fallos de comunicación, conservando el formulario cuando corresponda.

Los errores que suceden antes de iniciar Kestrel, como puertos ocupados, errores de compilación o configuración de arranque, no atraviesan el pipelineHTTP y no pueden resolverse con middleware. Para ellos se debe detener la instancia previa y revisar el mensaje de inicio. No se dejan procesos de prueba ejecutándose al finalizar esta verificación.

Verificación del manejo ampliado:55 pruebas aprobadas, incluidas respuestas404/405, JSON inválido, timeout, antifalsificación, referencias escapadas, cancelación, HEAD, respuesta iniciada y los escenariosSQL con rollback.

## Límite de especie sin interrupción del depurador

`RegistrarMascota` devuelve el valor bigint `-2` cuando ya existen dos mascotas de la especie; los registros exitosos devuelven un ID positivo. El rechazo no inserta filas ni lanza `THROW50002`, y conserva el bloqueo del conteo durante la transacción. La API convierte ese resultado en409 con el mensaje de límite. La web muestra la alerta en el formulario y conserva los campos y el selector.

Esto evita la pausa de Visual Studio por una SqlException esperada que antes dejaba a la web esperando hasta su timeout. Las excepciones técnicas reales continúan protegidas por el middleware. Después de actualizar los procedimientos y recompilar, iniciar una sola instancia de cada proyecto desde Visual Studio.

Verificación:56 pruebas aprobadas. La prueba integral comprueba el alta de dos mascotas, la alerta al rechazar la tercera, la ausencia de una tercera fila y una consulta posterior exitosa; todos los datos de prueba se revierten.

## Cédula repetida sin excepciónSQL

`RegistrarCliente` devuelve bigint `-1` si la cédula ya existe (incluidos espacios exteriores); un ID positivo indica éxito. La comprobación conserva el bloqueo exclusivo y la transacción, no inserta una fila duplicada y no usa `THROW50001`. La API devuelve409 y la web muestra «No se puede registrar el cliente: ya existe un cliente con esa cédula.», conservando los datos del formulario.

Las pruebas verifican el resultado sin excepción, una sola fila para la cédula, una consulta posterior exitosa y la continuidad del registro de mascotas después de mostrar la alerta. Los fallos técnicos reales siguen protegidos por el middleware.
