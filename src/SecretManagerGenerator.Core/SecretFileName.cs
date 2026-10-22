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
