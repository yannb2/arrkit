using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Arrkit.Hosting;

/// <summary>
/// Keeps ASP.NET Core's data-protection keys (they sign the antiforgery tokens in every form) in
/// the app's /config folder, so they survive a container update.
/// </summary>
/// <remarks>
/// Without this, each new container makes new keys in its own home folder. A browser tab left open
/// across an update then logs "DefaultAntiforgery: An exception was thrown while deserializing the
/// token", and every start warns "Storing keys in a directory ... that may not be persisted outside
/// of the container".
/// </remarks>
public static class DataProtectionKeys
{
    /// <summary>The subfolder of the config folder that holds the key files.</summary>
    public const string FolderName = "keys";

    /// <summary>
    /// Store the keys in <c>{configFolder}/keys</c>. Returns that folder, or null when it can't be
    /// written (no /config mount, wrong owner): the app then keeps ASP.NET's default and still runs.
    /// </summary>
    public static string? PersistKeysIn(IHostApplicationBuilder builder, string configFolder, string appName)
    {
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName(appName);
        var keys = Path.Combine(configFolder, FolderName);
        if (!CanWrite(keys))
        {
            return null;
        }

        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keys));

        // Linux has no DPAPI, so the keys are stored unencrypted: they only sign antiforgery tokens
        // on the LAN, and /config is media-only (check_folders.py). Keep that one warning out of the logs.
        builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager", LogLevel.Error);
        return keys;
    }

    private static bool CanWrite(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var probe = Path.Combine(folder, $".write-test-{Environment.ProcessId}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
