using System;

namespace ArmRateParser.TestHarness
{
    internal class Program
    {
        private const string _outputDirectory = @"c:\users\pj\documents\armindex\output";

        private static void Main(string[] args)
        {
            //ProcessByRssFeed("http://www.federalreserve.gov/feeds/Data/H15_H15.XML");

            //var sourcesPath = @"C:\Users\pj\Documents\ArmIndex\ArmIndexRateSources.json";
            //dynamic sources = Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(sourcesPath));

            //foreach (var source in sources)
            //{
            //var currentThing = $"{source.Source} - {source.Description}";
            var res = ArmRateParser.Processor.ProcessWebSite("http://www.freddiemac.com/pmms/archive.html");
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(res));
            //}
        }
    }
}