using Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Tests;

public sealed class StartupConfigurationValidatorTests
{
    [Fact]
    public void LocalEnvironmentParserReadsQuotedAndExportedValues()
    {
        var values = LocalEnvironmentFile.Parse(
        [
            "# Local values",
            "ConnectionStrings__DefaultConnection=\"Host=127.0.0.1;Database=bigsamgl\"",
            "export Jwt__Secret='a-local-secret-with-more-than-thirty-two-bytes'",
            "INVALID NAME=value",
            "MISSING_SEPARATOR"
        ]);

        Assert.Equal("Host=127.0.0.1;Database=bigsamgl",
            values["ConnectionStrings__DefaultConnection"]);
        Assert.Equal("a-local-secret-with-more-than-thirty-two-bytes",
            values["Jwt__Secret"]);
        Assert.DoesNotContain("INVALID NAME", values.Keys);
    }

    [Fact]
    public void ProductionFailsClosedForLocalDatabaseAutomaticMigrationsAndWildcardHost()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Database=bigsamgl;Username=app;Password=unsafe;SSL Mode=Disable",
                ["Jwt:Secret"] = "a-production-secret-longer-than-thirty-two-bytes",
                ["Jwt:Issuer"] = "BigsamGL.Api",
                ["Jwt:Audience"] = "BigsamGL.Web",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["AllowedHosts"] = "*",
                ["Runtime:EnableSwagger"] = "true",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Database:InitializeOperationalData"] = "true",
                ["ForwardedHeaders:Enabled"] = "false"
            }).Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            StartupConfigurationValidator.Validate(configuration,
                new TestEnvironment(AppEnvironments.Production)));

        Assert.Contains("Production cannot use a loopback PostgreSQL host", exception.Message);
        Assert.Contains("Runtime:EnableSwagger must be false", exception.Message);
        Assert.Contains("ApplyMigrationsOnStartup must be false", exception.Message);
        Assert.Contains("Production AllowedHosts", exception.Message);
        Assert.Contains("DataProtection:KeysPath", exception.Message);
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
