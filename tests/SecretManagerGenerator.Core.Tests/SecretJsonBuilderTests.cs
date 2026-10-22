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
