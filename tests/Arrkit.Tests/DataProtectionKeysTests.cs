using Arrkit.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Arrkit.Tests;

[TestClass]
public sealed class DataProtectionKeysTests
{
    [TestMethod]
    public void KeysAreWrittenInTheConfigFolderAndReusedAfterARestart()
    {
        var config = Directory.CreateTempSubdirectory("arrkit-dp-").FullName;
        try
        {
            var first = Protect(config, "hello");
            Assert.IsNotEmpty(Directory.GetFiles(Path.Combine(config, DataProtectionKeys.FolderName), "key-*.xml"));

            // A second "container" with the same folder can read what the first one wrote.
            Assert.AreEqual("hello", Unprotect(config, first));
        }
        finally
        {
            Directory.Delete(config, recursive: true);
        }
    }

    [TestMethod]
    public void AFolderThatCannotBeWrittenKeepsTheDefault()
    {
        var file = Path.GetTempFileName(); // a file, so "{file}/keys" can't be made
        try
        {
            var builder = WebApplication.CreateBuilder();
            Assert.IsNull(DataProtectionKeys.PersistKeysIn(builder, file, "Test"));
            using var app = builder.Build();
            Assert.IsNotNull(app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("still works"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string Protect(string config, string text) => Provider(config).Protect(text);

    private static string Unprotect(string config, string text) => Provider(config).Unprotect(text);

    private static IDataProtector Provider(string config)
    {
        var builder = WebApplication.CreateBuilder();
        Assert.AreEqual(Path.Combine(config, DataProtectionKeys.FolderName), DataProtectionKeys.PersistKeysIn(builder, config, "Test"));
        var app = builder.Build();
        return app.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");
    }
}
