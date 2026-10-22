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
    public void Parse_InvalidXml_ReportsLineAndPosition()
    {
        var ex = Assert.Throws<ConfigParseException>(() => ConfigParser.Parse("<configuration>\n  <appSettings>\n</configuration>"));
        Assert.Equal("El archivo no es un XML válido (línea 3, posición 3).", ex.Message);
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
