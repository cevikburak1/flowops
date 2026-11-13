using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace FlowOps.Api.Tests;

public sealed class DeploymentTests
{
    [Theory]
    [InlineData("http://flowops.example", "https://identity.example/realms/flowops")]
    [InlineData("https://flowops.example/app", "https://identity.example/realms/flowops")]
    [InlineData("https://flowops.example", "http://identity.example/realms/flowops")]
    public void InvalidProductionOriginsAreRejected(string origin, string authority) =>
        Assert.Throws<InvalidOperationException>(() => AuthDeployment.Validate(new Uri(origin), authority, Path.GetTempPath()));

    [Fact]
    public void IndependentInstancesCanReadTheSamePersistedKeys()
    {
        const string origin = "https://flowops.example";
        const string issuer = "https://identity.example/realms/flowops";
        var directory = Directory.CreateTempSubdirectory("flowops-keys-");
        try
        {
            Assert.Throws<InvalidOperationException>(() => AuthDeployment.Validate(new Uri(origin), issuer, "relative-keys"));
            AuthDeployment.Validate(new Uri(origin), issuer, directory.FullName);
            var first = DataProtectionProvider.Create(directory, options => options.SetApplicationName("FlowOps"));
            var protectedValue = first.CreateProtector("flowops-test").Protect("pending invitation");
            var second = DataProtectionProvider.Create(directory, options => options.SetApplicationName("FlowOps"));
            Assert.Equal("pending invitation", second.CreateProtector("flowops-test").Unprotect(protectedValue));
        }
        finally
        {
            foreach (var file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
