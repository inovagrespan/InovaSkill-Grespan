using InovaSkill.Importer.Infrastructure.Configuration;

namespace InovaSkill.Importer.Tests.Configuration;

public sealed class DotEnvLoaderTests
{
    [Fact]
    public void Parse_skips_comments_and_invalid_lines_and_supports_quotes()
    {
        var values = DotEnvLoader.Parse(
        [
            "# comentário",
            "OPENAI_API_KEY=abc123",
            "export DATABASE_URL=\"postgres://localhost/db\"",
            "MESSAGE='Olá mundo' # comentário",
            "INVALID LINE",
            "1_INVALID=value",
        ]);

        Assert.Equal("abc123", values["OPENAI_API_KEY"]);
        Assert.Equal("postgres://localhost/db", values["DATABASE_URL"]);
        Assert.Equal("Olá mundo", values["MESSAGE"]);
        Assert.DoesNotContain("INVALID LINE", values.Keys);
        Assert.DoesNotContain("1_INVALID", values.Keys);
    }

    [Fact]
    public void Parse_decodes_double_quoted_escapes_without_touching_single_quotes()
    {
        var values = DotEnvLoader.Parse(
        [
            "DOUBLE=\"linha1\\nlinha2\\t\\\"ok\\\"\"",
            "SINGLE='linha\\nliteral'",
        ]);

        Assert.Equal("linha1\nlinha2\t\"ok\"", values["DOUBLE"]);
        Assert.Equal("linha\\nliteral", values["SINGLE"]);
    }

    [Fact]
    public void Load_does_not_override_an_existing_environment_variable()
    {
        const string key = "DOTENV_LOADER_TEST_KEY";
        var previous = Environment.GetEnvironmentVariable(key);
        var directory = Path.Combine(Path.GetTempPath(), $"dotenv-loader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, ".env"), $"{key}=from-file\n");
            Environment.SetEnvironmentVariable(key, "from-environment");

            DotEnvLoader.Load(directory);

            Assert.Equal("from-environment", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, previous);
            Directory.Delete(directory, recursive: true);
        }
    }
}
