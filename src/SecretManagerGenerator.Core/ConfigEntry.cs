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
