# Secret Manager Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Herramienta Blazor WebAssembly que convierte las secciones appSettings, connectionStrings y mailSettings de un web.config/app.config en un JSON plano key/value para un secret de AWS Secrets Manager por environment.

**Architecture:** Librería `Core` sin dependencias de UI con tres piezas (`ConfigParser`, `SecretJsonBuilder`, `SecretFileName`), cubierta con xUnit. Proyecto Blazor WebAssembly standalone con una sola página que llama a Core; todo se procesa en el navegador.

**Tech Stack:** .NET 10, Blazor WebAssembly standalone, System.Xml.Linq, System.Text.Json, xUnit, Bootstrap (incluido en el template).

**Spec:** `docs/superpowers/specs/2026-10-09-secret-manager-generator-design.md`

**Nota:** la carpeta no es un repositorio git, así que el plan no incluye pasos de commit.

---

## File Structure

| Archivo | Responsabilidad |
|---|---|
| `src/SecretManagerGenerator.Core/ConfigEntry.cs` | Tipos: `ConfigSection`, `ConfigEntry`, `ConfigParseResult`, `ConfigParseException` |
| `src/SecretManagerGenerator.Core/ConfigParser.cs` | XML → entradas + avisos |
| `src/SecretManagerGenerator.Core/SecretJsonBuilder.cs` | Entradas → JSON plano |
| `src/SecretManagerGenerator.Core/SecretFileName.cs` | Nombre del archivo de descarga |
| `tests/SecretManagerGenerator.Core.Tests/*Tests.cs` | Una clase de pruebas por pieza de Core |
| `src/SecretManagerGenerator.Web/Pages/Home.razor` | Pantalla única |
| `src/SecretManagerGenerator.Web/Layout/MainLayout.razor` | Layout mínimo, sin menú |
| `src/SecretManagerGenerator.Web/wwwroot/js/download.js` | Descarga del JSON desde el navegador |
| `samples/Web.config` | Archivo de ejemplo con valores ficticios para probar a mano |

---

### Task 1: Scaffold de la solución

**Files:**
- Create: solución, `src/SecretManagerGenerator.Core`, `src/SecretManagerGenerator.Web`, `tests/SecretManagerGenerator.Core.Tests`

- [ ] **Step 1: Crear solución y proyectos**

Desde `C:\Users\sagar\Desktop\SecretManagerGenerator`:

```powershell
dotnet new sln -n SecretManagerGenerator
dotnet new classlib -o src/SecretManagerGenerator.Core
dotnet new blazorwasm -o src/SecretManagerGenerator.Web
dotnet new xunit -o tests/SecretManagerGenerator.Core.Tests
dotnet sln add src/SecretManagerGenerator.Core src/SecretManagerGenerator.Web tests/SecretManagerGenerator.Core.Tests
dotnet add src/SecretManagerGenerator.Web reference src/SecretManagerGenerator.Core
dotnet add tests/SecretManagerGenerator.Core.Tests reference src/SecretManagerGenerator.Core
```

- [ ] **Step 2: Borrar los archivos de ejemplo de los templates**

```powershell
Remove-Item src/SecretManagerGenerator.Core/Class1.cs, tests/SecretManagerGenerator.Core.Tests/UnitTest1.cs
```

- [ ] **Step 3: Compilar**

Run: `dotnet build`
Expected: `Build succeeded` con 0 errores.

---

### Task 2: Tipos de Core y ConfigParser

**Files:**
- Create: `src/SecretManagerGenerator.Core/ConfigEntry.cs`
- Create: `src/SecretManagerGenerator.Core/ConfigParser.cs`
- Test: `tests/SecretManagerGenerator.Core.Tests/ConfigParserTests.cs`

- [ ] **Step 1: Crear los tipos**

`src/SecretManagerGenerator.Core/ConfigEntry.cs`:

```csharp
namespace SecretManagerGenerator.Core;

public enum ConfigSection
{
    AppSettings,
    ConnectionStrings,
    MailSettings
}

public sealed record ConfigEntry(ConfigSection Section, string SecretKey, string Value);

public sealed record ConfigParseResult(IReadOnlyList<ConfigEntry> Entries, IReadOnlyList<string> Warnings);

public sealed class ConfigParseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
```

- [ ] **Step 2: Escribir las pruebas (fallan)**

`tests/SecretManagerGenerator.Core.Tests/ConfigParserTests.cs`:

```csharp
using SecretManagerGenerator.Core;

namespace SecretManagerGenerator.Core.Tests;

public class ConfigParserTests
{
    private const string FullConfig = """
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <appSettings>
            <add key="webpages:Version" value="3.0.0.0" />
            <add key="ewallet-travel-side-Token" value="abc123" />
            <!-- <add key="OldToken" value="old" /> -->
          </appSettings>
          <connectionStrings>
            <!-- <add name="DefaultConnection" connectionString="Server=prod;Database=App;" providerName="System.Data.SqlClient" /> -->
            <add name="DefaultConnection" connectionString="Server=dev;Database=App;" providerName="System.Data.SqlClient" />
          </connectionStrings>
          <system.net>
            <mailSettings>
              <smtp from="no-reply@example.com" deliveryMethod="Network">
                <network host="smtp.sendgrid.net" userName="apikey" password="secret" port="587" />
              </smtp>
            </mailSettings>
          </system.net>
          <system.web>
            <compilation debug="true" targetFramework="4.8" />
          </system.web>
        </configuration>
        """;

    [Fact]
    public void Parse_FullConfig_ReturnsOnlyTheThreeSectionsWithPrefixedKeys()
    {
        var result = ConfigParser.Parse(FullConfig);

        ConfigEntry[] expected =
        [
            new(ConfigSection.AppSettings, "AppSettings_webpages:Version", "3.0.0.0"),
            new(ConfigSection.AppSettings, "AppSettings_ewallet-travel-side-Token", "abc123"),
            new(ConfigSection.ConnectionStrings, "ConnectionStrings_DefaultConnection_connectionString", "Server=dev;Database=App;"),
            new(ConfigSection.MailSettings, "MailSettings_SMTP_Network_host", "smtp.sendgrid.net"),
            new(ConfigSection.MailSettings, "MailSettings_SMTP_Network_userName", "apikey"),
            new(ConfigSection.MailSettings, "MailSettings_SMTP_Network_password", "secret"),
            new(ConfigSection.MailSettings, "MailSettings_SMTP_Network_port", "587"),
        ];
        Assert.Equal(expected, result.Entries);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Parse_KeepsKeyNameAndAttributeCasingFromFile()
    {
        var result = ConfigParser.Parse("""
            <configuration>
              <appSettings><add key="MiXeD-Key" value="1" /></appSettings>
              <connectionStrings><add name="myDb" ConnectionString="x" /></connectionStrings>
              <system.net><mailSettings><smtp><network Host="h" UserName="u" /></smtp></mailSettings></system.net>
            </configuration>
            """);

        Assert.Equal(
            [
                "AppSettings_MiXeD-Key",
                "ConnectionStrings_myDb_ConnectionString",
                "MailSettings_SMTP_Network_Host",
                "MailSettings_SMTP_Network_UserName",
            ],
            result.Entries.Select(e => e.SecretKey));
    }

    [Fact]
    public void Parse_FindsStructuralElementsIgnoringCase()
    {
        var result = ConfigParser.Parse("""
            <Configuration>
              <AppSettings><Add Key="A" Value="1" /></AppSettings>
            </Configuration>
            """);

        Assert.Equal([new ConfigEntry(ConfigSection.AppSettings, "AppSettings_A", "1")], result.Entries);
    }

    [Fact]
    public void Parse_AddWithoutValue_ProducesEmptyValue()
    {
        var result = ConfigParser.Parse("""
            <configuration>
              <appSettings><add key="A" /></appSettings>
              <connectionStrings><add name="Db" /></connectionStrings>
            </configuration>
            """);

        Assert.Equal(
            [
                new ConfigEntry(ConfigSection.AppSettings, "AppSettings_A", ""),
                new ConfigEntry(ConfigSection.ConnectionStrings, "ConnectionStrings_Db_connectionString", ""),
            ],
            result.Entries);
    }

    [Fact]
    public void Parse_IgnoresRemoveAndClear()
    {
        var result = ConfigParser.Parse("""
            <configuration>
              <appSettings><clear /><remove key="A" /><add key="B" value="2" /></appSettings>
            </configuration>
            """);

        Assert.Equal(["AppSettings_B"], result.Entries.Select(e => e.SecretKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("esto no es xml")]
    [InlineData("<configuration><appSettings></configuration>")]
    public void Parse_InvalidXml_Throws(string xml)
    {
        var ex = Assert.Throws<ConfigParseException>(() => ConfigParser.Parse(xml));
        Assert.Contains("XML válido", ex.Message);
    }

    [Fact]
    public void Parse_RootIsNotConfiguration_Throws()
    {
        var ex = Assert.Throws<ConfigParseException>(() => ConfigParser.Parse("<settings />"));
        Assert.Contains("<configuration>", ex.Message);
    }

    [Fact]
    public void Parse_MissingSections_WarnsAndReturnsTheRest()
    {
        var result = ConfigParser.Parse("""
            <configuration>
              <appSettings><add key="A" value="1" /></appSettings>
            </configuration>
            """);

        Assert.Single(result.Entries);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.Contains("<connectionStrings>"));
        Assert.Contains(result.Warnings, w => w.Contains("<mailSettings>"));
    }

    [Fact]
    public void Parse_NoKeys_Warns()
    {
        var result = ConfigParser.Parse("<configuration />");

        Assert.Empty(result.Entries);
        Assert.Contains(result.Warnings, w => w.Contains("ninguna key"));
    }

    [Fact]
    public void Parse_DuplicateKey_LastWinsAndWarns()
    {
        var result = ConfigParser.Parse("""
            <configuration>
              <appSettings>
                <add key="A" value="first" />
                <add key="B" value="2" />
                <add key="A" value="last" />
              </appSettings>
            </configuration>
            """);

        Assert.Equal(
            [
                new ConfigEntry(ConfigSection.AppSettings, "AppSettings_A", "last"),
                new ConfigEntry(ConfigSection.AppSettings, "AppSettings_B", "2"),
            ],
            result.Entries);
        Assert.Contains(result.Warnings, w => w.Contains("AppSettings_A") && w.Contains("duplicada"));
    }

    [Fact]
    public void Parse_ExternalSources_Warn()
    {
        var result = ConfigParser.Parse("""
            <configuration>
              <appSettings file="private.config"><add key="A" value="1" /></appSettings>
              <connectionStrings configSource="connections.config" />
            </configuration>
            """);

        Assert.Equal(["AppSettings_A"], result.Entries.Select(e => e.SecretKey));
        Assert.Contains(result.Warnings, w => w.Contains("private.config"));
        Assert.Contains(result.Warnings, w => w.Contains("connections.config"));
    }

    [Fact]
    public void Parse_EncryptedSection_IsSkippedWithWarning()
    {
        var result = ConfigParser.Parse("""
            <configuration>
              <connectionStrings configProtectionProvider="RsaProtectedConfigurationProvider">
                <EncryptedData><CipherData><CipherValue>AAAA</CipherValue></CipherData></EncryptedData>
              </connectionStrings>
            </configuration>
            """);

        Assert.Empty(result.Entries);
        Assert.Contains(result.Warnings, w => w.Contains("<connectionStrings>") && w.Contains("cifrada"));
    }
}
```

- [ ] **Step 3: Ejecutar y comprobar que falla**

Run: `dotnet test`
Expected: error de compilación `CS0103: The name 'ConfigParser' does not exist`.

- [ ] **Step 4: Implementar ConfigParser**

`src/SecretManagerGenerator.Core/ConfigParser.cs`:

```csharp
using System.Xml;
using System.Xml.Linq;

namespace SecretManagerGenerator.Core;

public static class ConfigParser
{
    public static ConfigParseResult Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new ConfigParseException($"El archivo no es un XML válido: {ex.Message}", ex);
        }

        var root = document.Root!;
        if (!NameIs(root, "configuration"))
            throw new ConfigParseException("El archivo no tiene <configuration> como elemento raíz.");

        var entries = new List<ConfigEntry>();
        var warnings = new List<string>();

        ParseAppSettings(root, entries, warnings);
        ParseConnectionStrings(root, entries, warnings);
        ParseMailSettings(root, entries, warnings);

        if (entries.Count == 0)
            warnings.Add("No se encontró ninguna key en appSettings, connectionStrings ni mailSettings.");

        return new ConfigParseResult(entries, warnings);
    }

    private static void ParseAppSettings(XElement root, List<ConfigEntry> entries, List<string> warnings)
    {
        var section = Child(root, "appSettings");
        if (section is null)
        {
            warnings.Add("No se encontró la sección <appSettings>.");
            return;
        }

        if (!IsReadable(section, "appSettings", warnings))
            return;

        if (Attribute(section, "file") is { } file)
            warnings.Add($"La sección <appSettings> referencia el archivo externo \"{file.Value}\": sus keys no están incluidas.");

        foreach (var add in Children(section, "add"))
        {
            var key = Attribute(add, "key")?.Value;
            if (string.IsNullOrEmpty(key))
                continue;

            var value = Attribute(add, "value")?.Value ?? "";
            AddEntry(entries, warnings, new ConfigEntry(ConfigSection.AppSettings, $"AppSettings_{key}", value));
        }
    }

    private static void ParseConnectionStrings(XElement root, List<ConfigEntry> entries, List<string> warnings)
    {
        var section = Child(root, "connectionStrings");
        if (section is null)
        {
            warnings.Add("No se encontró la sección <connectionStrings>.");
            return;
        }

        if (!IsReadable(section, "connectionStrings", warnings))
            return;

        foreach (var add in Children(section, "add"))
        {
            var name = Attribute(add, "name")?.Value;
            if (string.IsNullOrEmpty(name))
                continue;

            var connectionString = Attribute(add, "connectionString");
            var attributeName = connectionString?.Name.LocalName ?? "connectionString";
            AddEntry(entries, warnings, new ConfigEntry(
                ConfigSection.ConnectionStrings,
                $"ConnectionStrings_{name}_{attributeName}",
                connectionString?.Value ?? ""));
        }
    }

    private static void ParseMailSettings(XElement root, List<ConfigEntry> entries, List<string> warnings)
    {
        var systemNet = Child(root, "system.net");
        var section = systemNet is null ? null : Child(systemNet, "mailSettings");
        if (section is null)
        {
            warnings.Add("No se encontró la sección <mailSettings>.");
            return;
        }

        if (!IsReadable(section, "mailSettings", warnings))
            return;

        var smtp = Child(section, "smtp");
        if (smtp is not null && !IsReadable(smtp, "smtp", warnings))
            return;

        var network = smtp is null ? null : Child(smtp, "network");
        if (network is null)
        {
            warnings.Add("La sección <mailSettings> no tiene un elemento <smtp><network>.");
            return;
        }

        foreach (var attribute in network.Attributes().Where(a => !a.IsNamespaceDeclaration))
        {
            AddEntry(entries, warnings, new ConfigEntry(
                ConfigSection.MailSettings,
                $"MailSettings_SMTP_Network_{attribute.Name.LocalName}",
                attribute.Value));
        }
    }

    private static bool IsReadable(XElement section, string name, List<string> warnings)
    {
        if (Attribute(section, "configProtectionProvider") is not null)
        {
            warnings.Add($"La sección <{name}> está cifrada (configProtectionProvider) y no se puede leer; se omitió.");
            return false;
        }

        if (Attribute(section, "configSource") is { } configSource)
            warnings.Add($"La sección <{name}> usa configSource=\"{configSource.Value}\": esas keys viven en otro archivo y no están incluidas.");

        return true;
    }

    // Igual que .NET Framework: una key repetida conserva su posición y toma el último valor.
    private static void AddEntry(List<ConfigEntry> entries, List<string> warnings, ConfigEntry entry)
    {
        var index = entries.FindIndex(e => string.Equals(e.SecretKey, entry.SecretKey, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            entries.Add(entry);
            return;
        }

        entries[index] = entry;
        warnings.Add($"La key {entry.SecretKey} está duplicada; se usó el último valor.");
    }

    private static bool NameIs(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

    private static XElement? Child(XElement parent, string name) => Children(parent, name).FirstOrDefault();

    private static IEnumerable<XElement> Children(XElement parent, string name) =>
        parent.Elements().Where(e => NameIs(e, name));

    private static XAttribute? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(a =>
            !a.IsNamespaceDeclaration && string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 5: Ejecutar y comprobar que pasa**

Run: `dotnet test`
Expected: todas las pruebas de `ConfigParserTests` en verde.

---

### Task 3: SecretJsonBuilder

**Files:**
- Create: `src/SecretManagerGenerator.Core/SecretJsonBuilder.cs`
- Test: `tests/SecretManagerGenerator.Core.Tests/SecretJsonBuilderTests.cs`

- [ ] **Step 1: Escribir las pruebas (fallan)**

`tests/SecretManagerGenerator.Core.Tests/SecretJsonBuilderTests.cs`:

```csharp
using System.Text.Json;
using SecretManagerGenerator.Core;

namespace SecretManagerGenerator.Core.Tests;

public class SecretJsonBuilderTests
{
    [Fact]
    public void Build_WritesFlatObjectOrderedBySectionThenFileOrder()
    {
        ConfigEntry[] entries =
        [
            new(ConfigSection.MailSettings, "MailSettings_SMTP_Network_host", "smtp"),
            new(ConfigSection.ConnectionStrings, "ConnectionStrings_Db_connectionString", "Server=x"),
            new(ConfigSection.AppSettings, "AppSettings_B", "2"),
            new(ConfigSection.AppSettings, "AppSettings_A", "1"),
        ];

        var json = SecretJsonBuilder.Build(entries);

        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().Select(p => (p.Name, Value: p.Value.GetString())).ToArray();
        Assert.Equal(
            [
                ("AppSettings_B", "2"),
                ("AppSettings_A", "1"),
                ("ConnectionStrings_Db_connectionString", "Server=x"),
                ("MailSettings_SMTP_Network_host", "smtp"),
            ],
            properties);
        Assert.Contains("\n", json);
    }

    [Fact]
    public void Build_EscapesOnlyWhatJsonRequires()
    {
        const string value = """Server=.\SQL;Password="a&b'<c>+";""";
        ConfigEntry[] entries = [new(ConfigSection.ConnectionStrings, "ConnectionStrings_Db_connectionString", value)];

        var json = SecretJsonBuilder.Build(entries);

        Assert.Contains("""Server=.\\SQL;Password=\"a&b'<c>+\";""", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(value, document.RootElement.GetProperty("ConnectionStrings_Db_connectionString").GetString());
    }

    [Fact]
    public void Build_NoEntries_WritesEmptyObject()
    {
        Assert.Equal("{}", SecretJsonBuilder.Build([]));
    }
}
```

- [ ] **Step 2: Ejecutar y comprobar que falla**

Run: `dotnet test`
Expected: error de compilación `CS0103: The name 'SecretJsonBuilder' does not exist`.

- [ ] **Step 3: Implementar**

`src/SecretManagerGenerator.Core/SecretJsonBuilder.cs`:

```csharp
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SecretManagerGenerator.Core;

public static class SecretJsonBuilder
{
    // Relaxed: deja &, ', <, > y + legibles en vez de \uXXXX; el resultado sigue siendo JSON válido.
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Build(IEnumerable<ConfigEntry> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var entry in entries.OrderBy(e => e.Section))
                writer.WriteString(entry.SecretKey, entry.Value);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
```

- [ ] **Step 4: Ejecutar y comprobar que pasa**

Run: `dotnet test`
Expected: todas las pruebas en verde.

---

### Task 4: SecretFileName

**Files:**
- Create: `src/SecretManagerGenerator.Core/SecretFileName.cs`
- Test: `tests/SecretManagerGenerator.Core.Tests/SecretFileNameTests.cs`

- [ ] **Step 1: Escribir las pruebas (fallan)**

`tests/SecretManagerGenerator.Core.Tests/SecretFileNameTests.cs`:

```csharp
using SecretManagerGenerator.Core;

namespace SecretManagerGenerator.Core.Tests;

public class SecretFileNameTests
{
    [Theory]
    [InlineData("MiApp", "dev", "MiApp-dev.json")]
    [InlineData("  MiApp ", " prod  ", "MiApp-prod.json")]
    [InlineData("Mi App", "qa/1", "Mi-App-qa-1.json")]
    [InlineData("a\\b:c*d?e\"f<g>h|i", "dev", "a-b-c-d-e-f-g-h-i-dev.json")]
    public void For_BuildsSafeFileName(string application, string environment, string expected)
    {
        Assert.Equal(expected, SecretFileName.For(application, environment));
    }
}
```

- [ ] **Step 2: Ejecutar y comprobar que falla**

Run: `dotnet test`
Expected: error de compilación `CS0103: The name 'SecretFileName' does not exist`.

- [ ] **Step 3: Implementar**

`src/SecretManagerGenerator.Core/SecretFileName.cs`:

```csharp
namespace SecretManagerGenerator.Core;

public static class SecretFileName
{
    // Lista fija: Path.GetInvalidFileNameChars depende de la plataforma y en WebAssembly casi no excluye nada.
    private const string InvalidChars = "\\/:*?\"<>|";

    public static string For(string application, string environment) =>
        $"{Sanitize(application)}-{Sanitize(environment)}.json";

    private static string Sanitize(string value) =>
        string.Concat(value.Trim().Select(c =>
            char.IsWhiteSpace(c) || char.IsControl(c) || InvalidChars.Contains(c) ? '-' : c));
}
```

- [ ] **Step 4: Ejecutar y comprobar que pasa**

Run: `dotnet test`
Expected: todas las pruebas en verde.

---

### Task 5: Pantalla Blazor

**Files:**
- Delete: `src/SecretManagerGenerator.Web/Pages/Counter.razor`, `Pages/Weather.razor`, `Layout/NavMenu.razor`, `Layout/NavMenu.razor.css`, `Layout/MainLayout.razor.css`, `wwwroot/sample-data/`
- Modify: `src/SecretManagerGenerator.Web/Layout/MainLayout.razor` (reemplazo completo)
- Modify: `src/SecretManagerGenerator.Web/Pages/Home.razor` (reemplazo completo)
- Modify: `src/SecretManagerGenerator.Web/wwwroot/index.html` (título y script)
- Create: `src/SecretManagerGenerator.Web/wwwroot/js/download.js`

- [ ] **Step 1: Borrar las páginas de ejemplo del template**

```powershell
$web = "src/SecretManagerGenerator.Web"
Remove-Item "$web/Pages/Counter.razor", "$web/Pages/Weather.razor", "$web/Layout/NavMenu.razor", "$web/Layout/NavMenu.razor.css", "$web/Layout/MainLayout.razor.css"
Remove-Item "$web/wwwroot/sample-data" -Recurse
```

- [ ] **Step 2: Layout mínimo**

`src/SecretManagerGenerator.Web/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<main class="container py-4">
    @Body
</main>
```

- [ ] **Step 3: Script de descarga**

`src/SecretManagerGenerator.Web/wwwroot/js/download.js`:

```js
window.secretManagerGenerator = {
    downloadText: function (fileName, text) {
        const blob = new Blob([text], { type: "application/json" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    }
};
```

En `wwwroot/index.html`: cambiar el contenido de `<title>` a `Secret Manager Generator` y añadir `<script src="js/download.js"></script>` justo antes del `<script>` de `blazor.webassembly`.

- [ ] **Step 4: Página**

`src/SecretManagerGenerator.Web/Pages/Home.razor`:

```razor
@page "/"
@using SecretManagerGenerator.Core
@inject IJSRuntime JS

<PageTitle>Secret Manager Generator</PageTitle>

<h1 class="h3">Secret Manager Generator</h1>
<p class="text-muted">
    Sube un web.config o app.config y obtén el JSON key/value para crear el secret en AWS Secrets Manager.
    El archivo se procesa en tu navegador y no se envía a ningún servidor.
</p>

<div class="card mb-4">
    <div class="card-body">
        <h2 class="h5 card-title">1. Archivo de configuración</h2>
        <InputFile OnChange="LoadFile" accept=".config,.xml" class="form-control" />
    </div>
</div>

@if (error is not null)
{
    <div class="alert alert-danger" role="alert">@error</div>
}

@if (result is not null)
{
    if (result.Warnings.Count > 0)
    {
        <div class="alert alert-warning" role="alert">
            <ul class="mb-0">
                @foreach (var warning in result.Warnings)
                {
                    <li>@warning</li>
                }
            </ul>
        </div>
    }

    if (result.Entries.Count > 0)
    {
        <div class="card mb-4">
            <div class="card-body">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h2 class="h5 card-title mb-0">2. Keys detectadas (@result.Entries.Count)</h2>
                    <div class="form-check form-switch mb-0">
                        <input class="form-check-input" type="checkbox" id="showValues" @bind="showValues" />
                        <label class="form-check-label" for="showValues">Mostrar valores</label>
                    </div>
                </div>
                <table class="table table-sm align-middle mb-0">
                    <tbody>
                        @foreach (var group in result.Entries.GroupBy(e => e.Section).OrderBy(g => g.Key))
                        {
                            <tr class="table-light">
                                <th colspan="2">@group.Key</th>
                            </tr>
                            @foreach (var entry in group)
                            {
                                <tr>
                                    <td><code>@entry.SecretKey</code></td>
                                    <td class="text-break font-monospace small">@(showValues ? entry.Value : Mask)</td>
                                </tr>
                            }
                        }
                    </tbody>
                </table>
            </div>
        </div>

        <div class="card mb-4">
            <div class="card-body">
                <h2 class="h5 card-title">3. Secret</h2>
                <div class="row g-3">
                    <div class="col-md-6">
                        <label class="form-label" for="application">Nombre de la aplicación</label>
                        <input class="form-control" id="application" placeholder="MiApp" @bind="application" @bind:event="oninput" />
                    </div>
                    <div class="col-md-6">
                        <label class="form-label" for="environment">Environment</label>
                        <input class="form-control" id="environment" placeholder="dev" @bind="environment" @bind:event="oninput" />
                    </div>
                </div>
                @if (!CanGenerate)
                {
                    <p class="form-text mb-0 mt-3">Completa ambos campos para generar el JSON.</p>
                }
            </div>
        </div>

        if (CanGenerate)
        {
            var json = SecretJsonBuilder.Build(result.Entries);
            var downloadName = SecretFileName.For(application, environment);

            <div class="card mb-4">
                <div class="card-body">
                    <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
                        <div>
                            <h2 class="h5 card-title mb-1">4. JSON del secret</h2>
                            <div class="small text-muted">
                                Nombre sugerido del secret: <code>@application.Trim()/@environment.Trim()</code>
                            </div>
                        </div>
                        <div class="d-flex gap-2">
                            <button type="button" class="btn btn-outline-primary" @onclick="() => CopyAsync(json)">
                                @(copied ? "Copiado" : "Copiar")
                            </button>
                            <button type="button" class="btn btn-primary" @onclick="() => DownloadAsync(downloadName, json)">
                                Descargar @downloadName
                            </button>
                        </div>
                    </div>
                    <pre class="bg-light border rounded p-3 mb-0"><code>@(showValues ? json : MaskedJson())</code></pre>
                </div>
            </div>
        }
    }
}

@code {
    private const long MaxFileSize = 2 * 1024 * 1024;
    private const string Mask = "••••••••";

    private ConfigParseResult? result;
    private string? error;
    private bool showValues;
    private bool copied;
    private string application = "";
    private string environment = "";

    private bool CanGenerate => !string.IsNullOrWhiteSpace(application) && !string.IsNullOrWhiteSpace(environment);

    private async Task LoadFile(InputFileChangeEventArgs e)
    {
        result = null;
        error = null;
        copied = false;

        if (e.File.Size > MaxFileSize)
        {
            error = "El archivo supera el máximo de 2 MB.";
            return;
        }

        try
        {
            using var reader = new StreamReader(e.File.OpenReadStream(MaxFileSize));
            result = ConfigParser.Parse(await reader.ReadToEndAsync());
        }
        catch (ConfigParseException ex)
        {
            error = ex.Message;
        }
    }

    // Vista previa con los valores ocultos; copiar y descargar usan siempre el JSON real.
    private string MaskedJson() =>
        SecretJsonBuilder.Build(result!.Entries.Select(e => e with { Value = Mask }));

    private async Task CopyAsync(string json)
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", json);
        }
        catch (JSException)
        {
            error = "El navegador no permitió copiar al portapapeles. Usa el botón de descargar.";
            return;
        }

        copied = true;
        StateHasChanged();
        await Task.Delay(2000);
        copied = false;
    }

    private async Task DownloadAsync(string fileName, string json) =>
        await JS.InvokeVoidAsync("secretManagerGenerator.downloadText", fileName, json);
}
```

- [ ] **Step 5: Compilar**

Run: `dotnet build`
Expected: `Build succeeded` con 0 errores y 0 warnings nuevos.

---

### Task 6: Archivo de ejemplo y verificación en el navegador

**Files:**
- Create: `samples/Web.config`

- [ ] **Step 1: Crear el archivo de ejemplo (valores ficticios)**

`samples/Web.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <connectionStrings>
    <!-- PROD -->
    <!-- <add name="DefaultConnection" connectionString="Server=prod-sql;Database=MiApp;User Id=app;Password=prod-password;" providerName="System.Data.SqlClient" /> -->
    <!-- DEV -->
    <add name="DefaultConnection" connectionString="Server=.\SQLEXPRESS;Database=MiApp;User Id=app;Password=dev-password;" providerName="System.Data.SqlClient" />
  </connectionStrings>
  <appSettings>
    <add key="webpages:Version" value="3.0.0.0" />
    <add key="ClientValidationEnabled" value="true" />
    <add key="ewallet-travel-side-Token" value="token-de-ejemplo" />
    <!-- <add key="LegacyApiKey" value="no-debe-salir" /> -->
  </appSettings>
  <system.net>
    <mailSettings>
      <smtp from="no-reply@example.com">
        <network host="smtp.sendgrid.net" userName="apikey" password="sendgrid-password" port="587" />
      </smtp>
    </mailSettings>
  </system.net>
  <system.web>
    <compilation debug="true" targetFramework="4.8" />
    <httpRuntime targetFramework="4.8" />
  </system.web>
</configuration>
```

- [ ] **Step 2: Ejecutar la app**

Run: `dotnet run --project src/SecretManagerGenerator.Web`
Abrir la URL que imprime (`http://localhost:<puerto>`).

- [ ] **Step 3: Verificar en el navegador**

1. Subir `samples/Web.config`: aparecen 8 keys (3 AppSettings, 1 ConnectionStrings, 4 MailSettings), sin avisos, valores enmascarados.
2. No aparecen `LegacyApiKey`, el connection string de PROD, `providerName` ni `from`.
3. Escribir `MiApp` y `dev`: aparece el JSON, el nombre sugerido `MiApp/dev` y el botón `Descargar MiApp-dev.json`.
4. Activar *Mostrar valores*: tabla y JSON muestran los valores reales.
5. *Descargar* baja un archivo con el JSON real aunque la vista esté enmascarada.
6. Subir un archivo que no sea XML: aparece el error y desaparecen las tarjetas 2 a 4.

- [ ] **Step 4: Suite completa**

Run: `dotnet test`
Expected: todas las pruebas en verde.
