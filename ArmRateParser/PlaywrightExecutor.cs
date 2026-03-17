using Microsoft.Playwright;
using System.IO;
using System.Threading.Tasks;

namespace ArmRateParser
{
    internal static class PlaywrightExecutor
    {
        public static async Task ExecutePlaywrightAsync(string url, string imageOutputPath, string htmlOutputPath)
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync(url);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = imageOutputPath, FullPage = true });
            var content = await page.ContentAsync();
            await File.WriteAllTextAsync(htmlOutputPath, content);

            await browser.CloseAsync();
        }
    }
}
