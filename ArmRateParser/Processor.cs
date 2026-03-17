using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
            var overallDate = theDate != null ? DateTimeOffset.Parse(theDate.Value) : DateTimeOffset.MinValue;

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

        public static async Task<object> ProcessWebSiteAsync(string url, IEnumerable<string>? extraTagsToInclude = null)
        {
            await ArmRateParser.PlaywrightInstaller.EnsureChromiumInstalledAsync();

            var imageOutputPath = $"{Path.GetTempFileName()}.png";

            var htmlOutputPath = Path.GetTempFileName();

            var numberOfTimesIHaveTried = 0;

            HtmlNodeCollection? allTRtags = null;
            var xmlDataSetPath = string.Empty;
            var htmlDoc = new HtmlAgilityPack.HtmlDocument();
            var allTagsJsonFileName = string.Empty;
            string? playwrightErrorOutput = null;
            var succeeded = false;

            // Since some web sites are apparently really sketchy, try multiple times to get some 
            // valid-looking HTML output before we give up.
            while (numberOfTimesIHaveTried++ < WebSiteRetryCount && !succeeded)
            {
                // Use Playwright to render the page and produce a screenshot + HTML output
                try
                {
                    await PlaywrightExecutor.ExecutePlaywrightAsync(url, imageOutputPath, htmlOutputPath);
                    succeeded = true;
                }
                catch (System.Exception ex)
                {
                    playwrightErrorOutput = ex.ToString();

                    // this may be unnecessary - I found it helpful when we were previously using phantomjs to 
                    // render the pages because it apparently needed extra time when an error occurred. 
                    // But maybe Playwright doesn't have that issue.
                    await Task.Delay(300);
                }

                htmlDoc.Load(htmlOutputPath);

                // Some tables have THEAD, some have TBODY, etc. so it gets a bit tricky. But we SHOULD be able
                // to count on the fact that all the TD and TH tags are directly inside a row (TR). So find all the
                // TR tags, but group them by their "parent" node - again, which may be a <table> or <thead> or <tbody>
                // or WhateverTF. By grouping on the parent in this way, it should - pretty much - also treat the rows
                // so everything in one table is grouped together.
                allTRtags = htmlDoc.DocumentNode.SelectNodes("//tr");
            }

            ////////////////////////////////////////////////////////////////////
            // now jam all the HTML nodes into a document that
            // kinda has everything - this is referred to as "all tags." 
            // Although it's not truly "all," the idea is that it will contain
            // all the tags we need to care about.
            ////////////////////////////////////////////////////////////////////
            var currentLineNumber = 0;
            const string defaultAllTags = "//td|//th|//span|//div|//p|//h1|//h2|//h3|//h4";
            var allTagsQuery = defaultAllTags;
            // if the caller specified extra tags to include, add those to the XPath query that finds all the nodes we want to include in the "all tags" dump
            if (extraTagsToInclude != null)
            {
                var extraTagsQuery = string.Join("|", extraTagsToInclude.Select(t => $"//{t}"));
                allTagsQuery += "|" + extraTagsQuery;
            }
            var allTheNodes = htmlDoc.DocumentNode.SelectNodes(allTagsQuery);
            var dataToWrite = from n in allTheNodes
                              select new
                              {
                                  Content = Decrapify(n.InnerText),
                                  LineNumber = currentLineNumber++,
                                  Tag = n.Name
                              };
            allTagsJsonFileName = Path.GetTempFileName();
            File.WriteAllText(allTagsJsonFileName, Newtonsoft.Json.JsonConvert.SerializeObject(dataToWrite, Newtonsoft.Json.Formatting.Indented));

            if (allTRtags != null)
            {
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
                            
                        List<object> values = [currentRowNumber++, .. cells.Select(i => Decrapify(i.InnerText))];
                        // List<object> values = new List<object>();
                        // values.Add(currentRowNumber++);
                        // values.AddRange(cells.Select(i => Decrapify(i.InnerText)));
                        
                        dataTable.Rows.Add(values.ToArray());
                    }
                    data.Tables.Add(dataTable);
                }

                xmlDataSetPath = Path.GetTempFileName();
                //Path.Combine(outputDirectory, $"{name}-DataSet.xml");
                data.WriteXml(xmlDataSetPath, XmlWriteMode.WriteSchema);
            }

            return new
            {
                WebPageImagePath = imageOutputPath,
                WebPageHtmlPath = htmlOutputPath,
                AllTagsJsonPath = allTagsJsonFileName,
                DataSetXmlPath = xmlDataSetPath,
                PlaywrightError = playwrightErrorOutput
            };
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