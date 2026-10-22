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
