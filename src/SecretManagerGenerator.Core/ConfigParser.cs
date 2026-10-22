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
            // Sin ex.Message: en WebAssembly los textos del framework vienen recortados a claves de recurso.
            throw new ConfigParseException(
                $"El archivo no es un XML válido (línea {ex.LineNumber}, posición {ex.LinePosition}).", ex);
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
