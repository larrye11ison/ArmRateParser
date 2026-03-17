using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace ArmRateParser
{
    internal static class PlaywrightInstaller
    {
        private static readonly string MarkerFile = Path.Combine(AppContext.BaseDirectory, "playwright_last_install.txt");
        private static readonly TimeSpan InstallInterval = TimeSpan.FromDays(1);

        /// <summary>
        /// Ensures Chromium for Playwright is installed. Runs at most once per day.
        /// Tries the managed API first (via reflection to tolerate API differences),
        /// then falls back to running the Playwright CLI (`playwright install chromium`).
        /// </summary>
        public static async Task<bool> EnsureChromiumInstalledAsync()
        {
            try
            {
                if (File.Exists(MarkerFile))
                {
                    var last = File.GetLastWriteTimeUtc(MarkerFile);
                    if (DateTime.UtcNow - last < InstallInterval) return true;
                }

                var ok = false;

                // Try managed API via reflection (tolerant to API surface changes)
                try
                {
                    var playwrightType = Type.GetType("Microsoft.Playwright.Playwright, Microsoft.Playwright");
                    if (playwrightType != null)
                    {
                        // Try InstallAsync() parameterless
                        var installMethod = playwrightType.GetMethod("InstallAsync", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                        if (installMethod != null)
                        {
                            var task = (Task)installMethod.Invoke(null, null)!;
                            await task.ConfigureAwait(false);
                            ok = true;
                        }
                        else
                        {
                            // Try InstallAsync(InstallOptions) overload
                            var alt = playwrightType.GetMethod("InstallAsync", BindingFlags.Public | BindingFlags.Static);
                            if (alt != null)
                            {
                                var parameters = alt.GetParameters();
                                if (parameters.Length == 1)
                                {
                                    // Create InstallOptions with Browsers = new[] { "chromium" }
                                    var optionsType = parameters[0].ParameterType;
                                    var optionsInstance = optionsType.GetConstructor(Type.EmptyTypes)?.Invoke(null);
                                    if (optionsInstance != null)
                                    {
                                        var browsersProp = optionsType.GetProperty("Browsers");
                                        if (browsersProp != null && browsersProp.PropertyType.IsArray)
                                        {
                                            var elementType = browsersProp.PropertyType.GetElementType();
                                            var arr = Array.CreateInstance(elementType!, 1);
                                            arr.SetValue("chromium", 0);
                                            browsersProp.SetValue(optionsInstance, arr);
                                        }
                                        var t = (Task)alt.Invoke(null, new[] { optionsInstance })!;
                                        await t.ConfigureAwait(false);
                                        ok = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                    ok = false;
                }

                if (!ok)
                {
                    // Fallback 1: use Playwright CLI (playwright or playwright.cmd)
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "playwright",
                            Arguments = "install chromium",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                            await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                            await proc.WaitForExitAsync().ConfigureAwait(false);
                            ok = proc.ExitCode == 0;
                        }
                    }
                    catch
                    {
                        ok = false;
                    }
                }

                // Fallback 2: run generated playwright.ps1 in the output folder using pwsh or powershell
                if (!ok)
                {
                    try
                    {
                        var scriptPath = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");
                        if (File.Exists(scriptPath))
                        {
                            // prefer pwsh
                            var shell = File.Exists(Environment.ExpandEnvironmentVariables("%ProgramFiles%\\PowerShell\\7\\pwsh.exe")) ?
                                        Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramFiles%"), "PowerShell", "7", "pwsh.exe") :
                                        "pwsh";

                            var psi = new ProcessStartInfo
                            {
                                FileName = shell,
                                Arguments = $"-File \"{scriptPath}\" install chromium",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using var proc = Process.Start(psi);
                            if (proc != null)
                            {
                                await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                                await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);
                                await proc.WaitForExitAsync().ConfigureAwait(false);
                                ok = proc.ExitCode == 0;
                            }
                        }
                    }
                    catch
                    {
                        ok = false;
                    }
                }

                if (ok)
                {
                    File.WriteAllText(MarkerFile, DateTime.UtcNow.ToString("o"));
                }

                return ok;
            }
            catch
            {
                return false;
            }
        }
    }
}
