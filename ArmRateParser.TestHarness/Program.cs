using System;

namespace ArmRateParser.TestHarness
{
    internal class Program
    {
        private const string _outputDirectory = @"c:\users\pj\documents\armindex\output";

        private static async System.Threading.Tasks.Task Main(string[] args)
        {
            // Ensure Playwright Chromium is installed/updated daily before running

            //ProcessByRssFeed("http://www.federalreserve.gov/feeds/Data/H15_H15.XML");

            //var sourcesPath = @"C:\Users\pj\Documents\ArmIndex\ArmIndexRateSources.json";
            //dynamic sources = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(sourcesPath));

            //foreach (var source in sources)
            //{
            //var currentThing = $"{source.Source} - {source.Description}";

            //var res = await ArmRateParser.Processor.ProcessWebSiteAsync("https://www.wellsfargo.com/mortgage/cost-of-savings-index/");
            
            var res = await ArmRateParser.Processor.ProcessWebSiteAsync("https://www.wsj.com/market-data/bonds/moneyrates");
            
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(res));
            //}
        }
    }
}