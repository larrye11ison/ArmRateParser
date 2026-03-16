using System;
using System.IO;

namespace ArmRateParser
{
    internal static class PlaywrightConvenience
    {
        /// <summary>
        /// Uses PlaywrightSharp to navigate to a URL, save a screenshot and write the rendered HTML to disk.
        /// This method runs the async Playwright calls synchronously so it can be used from existing sync code.
        /// </summary>
        public static object ExecutePlaywright(string url, string imageOutputPath, string htmlOutputPath)
        {
            try
            {
                // Create Playwright and launch Chromium headless
                var playwright = PlaywrightSharp.Playwright.CreateAsync().GetAwaiter().GetResult();
                var browser = playwright.Chromium.LaunchAsync(new PlaywrightSharp.LaunchOptions { Headless = true }).GetAwaiter().GetResult();
                var page = browser.NewPageAsync().GetAwaiter().GetResult();

                page.GoToAsync(url).GetAwaiter().GetResult();

                // Take screenshot
                page.ScreenshotAsync(path: imageOutputPath).GetAwaiter().GetResult();

                // Get content and write to file
                var content = page.GetContentAsync().GetAwaiter().GetResult();
                File.WriteAllText(htmlOutputPath, content);

                browser.CloseAsync().GetAwaiter().GetResult();
                return content;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
}
