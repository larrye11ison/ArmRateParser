            
                {
                    System.Threading.Thread.Sleep(300);
                }
            }

            
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