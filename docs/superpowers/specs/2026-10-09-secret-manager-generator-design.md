# Secret Manager Generator — Diseño

Fecha: 2026-10-09

## Objetivo

Herramienta Blazor que recibe un `web.config` o `app.config` de un proyecto ASP.NET MVC (.NET Framework 4.8) y genera un JSON plano key/value con el contenido de tres secciones, listo para entregar al DevOps y pegarlo como valor de **un** secret de AWS Secrets Manager por environment (no un secret por key).

## Alcance

Entra:

- `<appSettings>`
- `<connectionStrings>`
- `<system.net><mailSettings><smtp><network>`

Queda fuera: cualquier otra sección del archivo, `providerName` de los connection strings, los atributos de `<smtp>` (`from`, `deliveryMethod`), transforms XDT (`Web.Release.config`), creación del secret en AWS y generación de comandos AWS CLI.

## Arquitectura

Solución .NET 10 `SecretManagerGenerator.sln` con tres proyectos:

| Proyecto | Tipo | Responsabilidad |
|---|---|---|
| `src/SecretManagerGenerator.Core` | Librería de clases | Parsear el XML y generar el JSON. Sin dependencias de Blazor. |
| `src/SecretManagerGenerator.Web` | Blazor WebAssembly standalone | Pantalla única. Solo llama a Core. |
| `tests/SecretManagerGenerator.Core.Tests` | xUnit | Pruebas de Core. |

Todo el procesamiento ocurre en el navegador: el archivo nunca se envía a un servidor.

### Core

- `ConfigEntry` — registro con `Section` (`AppSettings`, `ConnectionStrings`, `MailSettings`), `SecretKey` (nombre final en el secret) y `Value`.
- `ConfigParseResult` — lista de `ConfigEntry` más lista de avisos (`Warnings`).
- `ConfigParser.Parse(string xml)` — devuelve `ConfigParseResult`; lanza `ConfigParseException` si el XML es inválido o la raíz no es `<configuration>`.
- `SecretJsonBuilder.Build(IEnumerable<ConfigEntry>)` — devuelve el JSON indentado, objeto plano, en el orden: AppSettings, ConnectionStrings, MailSettings, y dentro de cada sección en el orden del archivo.
- `SecretFileName.For(application, environment)` — devuelve `{application}-{environment}.json`, sustituyendo los caracteres no válidos para nombre de archivo.

## Reglas de conversión

| Origen | Key en el secret | Valor |
|---|---|---|
| `<appSettings><add key="X" value="V">` | `AppSettings_X` | `V` |
| `<connectionStrings><add name="N" connectionString="C">` | `ConnectionStrings_N_connectionString` | `C` |
| `<smtp><network a="V" …>` (cada atributo) | `MailSettings_SMTP_Network_a` | `V` |

- El prefijo de sección es fijo (`AppSettings`, `ConnectionStrings`, `MailSettings:SMTP:Network`).
- La parte final (`key`, `name`, nombre de atributo) se copia **exactamente** como aparece en el archivo, sin cambiar mayúsculas ni minúsculas.
- De `<network>` se incluyen todos los atributos presentes (`host`, `userName`, `password`, `port`, `enableSsl`, …).
- Los elementos dentro de comentarios XML (`<!-- … -->`) se excluyen. Caso de uso: varios connection strings de distintos environments en el mismo archivo con solo uno descomentado.
- Los nombres de elementos y atributos estructurales (`appSettings`, `add`, `key`, `connectionString`, `network`, …) se localizan sin distinguir mayúsculas.
- `<add>` sin atributo `value` genera valor `""`. `<remove>` y `<clear>` se ignoran.

## Flujo en pantalla

1. **Subir archivo** (`.config` / `.xml`, máximo 2 MB).
2. **Keys detectadas**: tabla agrupada por sección con la key final y su valor. Valores enmascarados por defecto, con un control para mostrarlos. Encima, la lista de avisos si los hay.
3. **Secret**: campos *Nombre de la aplicación* y *Environment* (ambos obligatorios). No hay botón de generar: el resultado aparece en cuanto ambos tienen valor. El nombre del secret sugerido se muestra como `{aplicación}/{environment}`.
4. **Resultado**: JSON en pantalla con botones **Copiar** y **Descargar** (`{aplicación}-{environment}.json`).

Cambiar el environment o subir otro archivo regenera el resultado; no se guarda nada entre sesiones.

## Avisos y errores

| Situación | Comportamiento |
|---|---|
| XML inválido o raíz distinta de `<configuration>` | Error en pantalla; no se genera nada. |
| Archivo mayor a 2 MB | Error en pantalla. |
| Falta alguna de las tres secciones | Aviso; se genera con las que existan. |
| Ninguna key encontrada | Aviso; no se muestran las tarjetas de keys, secret ni resultado. |
| Key duplicada dentro de una sección | Gana la última (igual que .NET); aviso con el nombre. |
| Sección con `configSource` o `<appSettings file="…">` | Aviso: esas keys viven en otro archivo y no están incluidas. |
| Sección cifrada (`configProtectionProvider`) | Aviso: no se puede leer; se omite. |

## Pruebas

Unitarias en Core:

- Cada regla de conversión, incluida la conservación exacta de mayúsculas/minúsculas.
- Exclusión de elementos comentados.
- Exclusión de `providerName` y de atributos de `<smtp>`.
- Cada aviso y cada error de la tabla anterior.
- `SecretJsonBuilder`: orden, formato plano y escape correcto de caracteres especiales en valores (comillas, `\`, `;`, `&`).
- `SecretFileName`: saneado de caracteres inválidos.

La pantalla se verifica manualmente ejecutando la app con un `web.config` de ejemplo.
