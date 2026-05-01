using System.Runtime.InteropServices;
using Dawning.AgentOS.Application.Abstractions.Hosting;

namespace Dawning.AgentOS.Infrastructure.Hosting;

/// <summary>
/// Default <see cref="IAppDataPathProvider"/> for the local desktop
/// backend. Per ADR-024 §B1 it routes to a per-platform application-data
/// directory:
/// <list type="bullet">
///   <item><description>Windows — <c>%LOCALAPPDATA%\dawning-agent-os\agentos.db</c></description></item>
///   <item><description>macOS — <c>~/Library/Application Support/dawning-agent-os/agentos.db</c></description></item>
///   <item><description>Linux — <c>$XDG_DATA_HOME/dawning-agent-os/agentos.db</c> with fallback to <c>~/.local/share/dawning-agent-os/agentos.db</c></description></item>
/// </list>
/// </summary>
public sealed class AppDataPathProvider : IAppDataPathProvider
{
    private const string AppFolder = "dawning-agent-os";
    private const string DatabaseFile = "agentos.db";

    /// <inheritdoc />
    public string GetDatabasePath()
    {
        var directory = Path.Combine(ResolveAppDataRoot(), AppFolder);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, DatabaseFile);
    }

    private static string ResolveAppDataRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // XDG Base Directory spec: prefer $XDG_DATA_HOME, fallback to
            // ~/.local/share. Environment.GetFolderPath(LocalApplicationData)
            // on Linux returns ~/.local/share so it matches the fallback.
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return xdg;
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS: SpecialFolder.ApplicationData maps to
            // ~/Library/Application Support, which is the canonical
            // per-user app-data location on macOS.
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        // Windows + any other platform: SpecialFolder.LocalApplicationData
        // maps to %LOCALAPPDATA% (e.g. C:\Users\<user>\AppData\Local).
        return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    }
}
