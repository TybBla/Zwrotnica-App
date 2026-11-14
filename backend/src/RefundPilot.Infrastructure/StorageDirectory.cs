using Microsoft.Extensions.Logging;

namespace RefundPilot.Infrastructure;

public static class StorageDirectory
{
    /// <summary>
    /// Creates a data folder (database, key ring). A permission error names the setting to override, because the
    /// production defaults under /home/data only exist on Azure App Service.
    /// </summary>
    public static void Ensure(string path, string settingName, ILogger logger)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (UnauthorizedAccessException)
        {
            logger.LogCritical(
                "Cannot create data folder {Path} (access denied). Outside Azure App Service set {Setting} to a writable location.",
                path, settingName);
            throw;
        }
    }
}
