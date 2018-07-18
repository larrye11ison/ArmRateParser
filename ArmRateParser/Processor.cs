using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;

namespace ArmRateParser
{
    public class Processor
    {
        const int WebSiteRetryCount = 10;
        private static readonly System.Text.StringBuilder deCrapifyingStringBuilder = new System.Text.StringBuilder();
        private static readonly Regex removeRedundantWhitespaceRegex = new Regex(@"\s{2,}");

        // At one time, I was using these "magic characters" to try and strip off the garbage that
        // was appearing on some sites (the Fed Home Loan Bank of SF in particular). But then I kept
        // finding more and more crap, all the way down to ascii 160, which is an "a" with an accent
        // over it. This is why I ended up just writing the DeCrapify function below and hard-coded the
        // ascii 160 code into it. However, I thought it might be good to keep this array of char's
        // around in case we have to go back to this original strategery.
        //
        //private static readonly char[] whatTheActualFuck = { (char)0xE2, (char)0x20AC, (char)0x2039 };

        /// <summary>
        /// Processes data that can be downloaded via RSS feed from the treasury. Not currently used.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static object ProcessTreasuryInfoRssFeed(string url)
        {
            XDocument doc = XDocument.Load(url, LoadOptions.None);

            XNamespace def = "http://purl.org/rss/1.0/";
            XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
            XNamespace dc = "http://purl.org/dc/elements/1.1/";
            XNamespace cb = "http://www.cbwiki.net/wiki/index.php/Specification_1.1";
            var theDate = doc.Descendants(def + "channel")
                .Descendants(dc + "date")
                .FirstOrDefault();
            var overallDate = DateTimeOffset.Parse(theDate.Value);

            var items = doc.Descendants(def + "item")
                .Select(i => new
                {
                    Description = i.Descendants(def + "description").First().Value.Replace("\n", string.Empty),
                    Value = i.Descendants(cb + "value").First().Value,
                    Date = DateTimeOffset.Parse(i.Descendants(dc + "date").First().Value),
                    Observed = i.Descendants(cb + "observationPeriod").First().Value,
                    OverallDateOrSomething = overallDate
                });

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(items);
            var outPath = Path.GetTempFileName();
            File.WriteAllText(outPath, json);
            return new
            {
                RssJsonOutputPath = outPath
            };
        }

        public static object ProcessWebSite(string url)
        {
            string theScript = PhantomConvenience.JavaScriptTemplate;
            var cbPath = Path.GetDirectoryName(new Uri(typeof(Processor).Assembly.CodeBase).LocalPath);
            var phantomPath = Path.Combine(cbPath, "phantomjs.exe");

            if (File.Exists(phantomPath) == false)
            {
                throw new FileNotFoundException($"PhantomJS executable not found at: {phantomPath}");
            }

            var jsFile = Path.GetTempFileName();

            // Create a temp file path for the image... note that we are putting png on the end...
            // I discovered that phantomjs (or whatever it ends up using internally to render the
            // image) will SILENTLY FAIL if the path you give it doesn't have the extension on
            // the end. This only took me like ten hours (almost literally) to figure out.
            // ... OMFG I can't even...
            var imageOutputPath = $"{Path.GetTempFileName()}.png";

            var htmlOutputPath = Path.GetTempFileName();

            File.WriteAllText(jsFile, theScript);

            var numberOfTimesIHaveTried = 0;

            HtmlNodeCollection allTRtags = null;
            var xmlDataSetPath = string.Empty;
            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            var allTagsJsonFileName = string.Empty;
            object phantomConsoleOutput = null;

            // Since some web sites are apparently really sketchy, try multiple times to get some 
            // valid-looking HTML output before we give up.
            while (numberOfTimesIHaveTried++ < WebSiteRetryCount && allTRtags == null)
            {
                phantomConsoleOutput = PhantomConvenience.ExecutePhantomScript(phantomPath, jsFile, url, imageOutputPath, htmlOutputPath);

                htmlDoc.Load(htmlOutputPath);

                // Some tables have THEAD, some have TBODY, etc. so it gets a bit tricky. But we SHOULD be able
                // to count on the fact that all the TD and TH tags are directly inside a row (TR). So find all the
                // TR tags, but group them by their "parent" node - again, which may be a <table> or <thead> or <tbody>
                // or WhateverTF. By grouping on the parent in this way, it should - pretty much - also treat the rows
                // so everything in one table is grouped together.
                allTRtags = htmlDoc.DocumentNode.SelectNodes("//tr");
                if (allTRtags == null)
                {
                    System.Threading.Thread.Sleep(300);
                }
            }

            if (allTRtags == null)
            {
                return new
                {
                    WebPageImagePath = imageOutputPath,
                    WebPageHtmlPath = htmlOutputPath,
                    AllTagsJsonPath = (string)null,//allTagsJsonFileName,
                    DataSetXmlPath = (string)null,//xmlDataSetPath,
                    PhantomConsoleOutput = phantomConsoleOutput
                };
            }
            var groupedByTables = allTRtags.GroupBy(t => t.ParentNode.XPath);

            var data = new DataSet();
            foreach (var tableGroup in groupedByTables)
            {
                var dataTable = new DataTable();
                dataTable.Columns.Add("rowid", typeof(int));

                var currentRowNumber = 0;
                foreach (var row in tableGroup)
                {
                    var cells = row.SelectNodes("td|th");
                    if (cells == null || cells.Count == 0)
                        continue;

                    while (dataTable.Columns.Count < cells.Count() + 1)
                        dataTable.Columns.Add($"Column_{dataTable.Columns.Count}", typeof(string));
                    List<object> values = new List<object>();
                    values.Add(currentRowNumber++);
                    values.AddRange(cells.Select(i => Decrapify(i.InnerText)));
                    dataTable.Rows.Add(values.ToArray());
                }
                data.Tables.Add(dataTable);
            }

            xmlDataSetPath = Path.GetTempFileName();
            //Path.Combine(outputDirectory, $"{name}-DataSet.xml");
            data.WriteXml(xmlDataSetPath, XmlWriteMode.WriteSchema);

            ////////////////////////////////////////////////////////////////////
            // now jam all the HTML nodes into a document that
            // kinda has everything - on some sites, data exists outside
            // the <table>, <td> and <th> tags
            ////////////////////////////////////////////////////////////////////
            var currentLineNumber = 0;
            var allTheNodes = htmlDoc.DocumentNode.SelectNodes("//td|//th|//span|//div|//p|//h1|//h2|//h3|//h4");
            var dataToWrite = from n in allTheNodes
                              select new
                              {
                                  Content = Decrapify(n.InnerText),
                                  LineNumber = currentLineNumber++,
                                  Tag = n.Name
                              };
            allTagsJsonFileName = Path.GetTempFileName();
            File.WriteAllText(allTagsJsonFileName, Newtonsoft.Json.JsonConvert.SerializeObject(dataToWrite, Newtonsoft.Json.Formatting.Indented));

            return new
            {
                WebPageImagePath = imageOutputPath,
                WebPageHtmlPath = htmlOutputPath,
                AllTagsJsonPath = allTagsJsonFileName,
                DataSetXmlPath = xmlDataSetPath,
                PhantomConsoleOutput = phantomConsoleOutput
            };

            //using (var phantomJS = new PhantomJS())
            //{
            //    phantomJS.OutputReceived += (sender, e) =>
            //    {
            //        Console.WriteLine("PhantomJS output: {0}", e.Data);
            //    };
            //    phantomJS.ErrorReceived += (sender, e) =>
            //    {
            //        Console.Error.WriteLine("PhantomJS error: {0}", e.Data);
            //    };

            //    string theScript = PhantomConvenience.JavaScriptTemplate;
            //    var cbPath = Path.GetDirectoryName(new Uri(typeof(Processor).Assembly.CodeBase).LocalPath);
            //    var phantomPath = Path.Combine(cbPath, "phantomjs.exe");

            //    if (File.Exists(phantomPath) == false)
            //    {
            //        throw new FileNotFoundException($"PhantomJS executable not found at: {phantomPath}");
            //    }

            //    phantomJS.PhantomJsExeName = phantomPath;
            //    var imageOutputPath = Path.GetTempFileName();
            //    Console.WriteLine($"Image output to: {imageOutputPath}");
            //    //Path.Combine(outputDirectory, $"{name}.png");
            //    var htmlOutputPath = Path.GetTempFileName();
            //    //Path.Combine(outputDirectory, $"{name}.html");
            //    phantomJS.RunScript(theScript, new string[] { url, imageOutputPath, htmlOutputPath });

            //    return null;

            //    var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            //    htmlDoc.Load(htmlOutputPath);

            //    // Some tables have THEAD, some have TBODY, etc. so it gets a bit tricky. But we SHOULD be able
            //    // to count on the fact that all the TD and TH tags are directly inside a row (TR). So find all the
            //    // TR tags, but group them by their "parent" node - again, which may be a <table> or <thead> or <tbody>
            //    // or WhateverTF. By grouping on the parent in this way, it should - pretty much - also treat the rows
            //    // so everything in one table is grouped together.
            //    var allTRtags = htmlDoc.DocumentNode.SelectNodes("//tr");
            //    var groupedByTables = allTRtags.GroupBy(t => t.ParentNode.XPath);

            //    var data = new DataSet();
            //    foreach (var tableGroup in groupedByTables)
            //    {
            //        var dataTable = new DataTable();
            //        dataTable.Columns.Add("rowid", typeof(int));

            //        var currentRowNumber = 0;
            //        foreach (var row in tableGroup)
            //        {
            //            var cells = row.SelectNodes("td|th");
            //            if (cells == null || cells.Count == 0)
            //                continue;

            //            while (dataTable.Columns.Count < cells.Count() + 1)
            //                dataTable.Columns.Add($"Column_{dataTable.Columns.Count}", typeof(string));
            //            List<object> values = new List<object>();
            //            values.Add(currentRowNumber++);
            //            values.AddRange(cells.Select(i => Decrapify(i.InnerText)));
            //            dataTable.Rows.Add(values.ToArray());
            //        }
            //        data.Tables.Add(dataTable);
            //    }

            //    var xmlDataSetPath = Path.GetTempFileName();
            //    //Path.Combine(outputDirectory, $"{name}-DataSet.xml");
            //    data.WriteXml(xmlDataSetPath, XmlWriteMode.WriteSchema);

            //    ////////////////////////////////////////////////////////////////////
            //    // now jam all the HTML nodes into a document that
            //    // kinda has everything - on some sites, data exists outside
            //    // the <table>, <td> and <th> tags
            //    ////////////////////////////////////////////////////////////////////
            //    var currentLineNumber = 0;
            //    var allTheNodes = htmlDoc.DocumentNode.SelectNodes("//td|//th|//span|//div|//p");
            //    var dataToWrite = from n in allTheNodes
            //                      select new
            //                      {
            //                          Content = Decrapify(n.InnerText),
            //                          LineNumber = currentLineNumber++,
            //                          Tag = n.Name
            //                      };
            //    var allTagsJsonFileName = Path.GetTempFileName();
            //    //Path.Combine(outputDirectory, $"{name}-tags.json");
            //    File.WriteAllText(allTagsJsonFileName, Newtonsoft.Json.JsonConvert.SerializeObject(dataToWrite, Newtonsoft.Json.Formatting.Indented));

            //    return new
            //    {
            //        WebPageImagePath = imageOutputPath,
            //        WebPageHtmlPath = htmlOutputPath,
            //        AllTagsJsonPath = allTagsJsonFileName,
            //        DataSetXmlPath = xmlDataSetPath
            //    };
            //}
        }

        /// <summary>
        /// Strips out ALL characters that are higher than ascii code 160 (0xA0), trims whitespace
        /// from beginning and end, plus removes redundant whitespace.
        /// </summary>
        /// <remarks>
        /// The Where() expression operates over the individual characters in the string, then the
        /// resulting enumerable sequence is "aggregated" back together using a string builder.
        /// Finally, redundant (and repetitive) whitespace is stripped off with the help
        /// of a regular expression.
        /// </remarks>
        /// <param name="inputString"></param>
        /// <returns></returns>
        private static string Decrapify(string inputString)
        {
            // HACK: some web site were giving me crap characters. This is SHITTY and I HATE IT, but it seems to be working so far...

            if (string.IsNullOrWhiteSpace(inputString)) return "";
            var intermediateResult = HttpUtility.HtmlDecode(inputString)

                .Where(c => (int)c < 160)
                .Aggregate(deCrapifyingStringBuilder.Clear(), (b, x) => b.Append(x.ToString()), b => b.ToString().Trim());
            return removeRedundantWhitespaceRegex.Replace(intermediateResult, " ");
        }
    }
}