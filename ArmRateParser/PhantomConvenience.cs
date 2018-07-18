using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ArmRateParser
{
    internal static class PhantomConvenience
    {
        public static readonly string JavaScriptTemplate = null;

        static PhantomConvenience()
        {
            // This is for convenience in our string template below, otherwise we'd have
            // to escape out all the curly braces in our jabbascript code, making it a bit
            // hard to read.
            const string LB = "{";
            const string RB = "}";

            JavaScriptTemplate = $@"
    var page = new WebPage();
    var fs = require('fs');
	var system = require('system');
	var args = system.args;

    var url             = args[1];
    var imageOutputPath = args[2];
    var htmlOutputPath  = args[3];

    console.log('using url               ' + url);
    console.log('using imageOutputPath   ' + imageOutputPath);
    console.log('using htmlOutputPath    ' + htmlOutputPath);

    page.onLoadFinished = function() {LB}
        //console.log('page load finished');
        page.render(imageOutputPath);
        fs.write(htmlOutputPath, page.content);
        phantom.exit();
    {RB};

    page.open(url, function() {LB}
        page.evaluate(function() {LB} {RB});
    {RB});";
        }

        /// <summary>
        /// Executes PhantomJS for a particular web site.
        /// </summary>
        /// <remarks>
        /// So, about the command-line parameters that are passed to PhantomJS...
        /// 
        /// OMFG...escaping double-quotes as command-line params is a fucking NIGHTMARE. Just do some
        /// googling and you'll be convinced. So if something includes a quote, this method will throw 
        /// an exception.
        ///
        /// Apparently, the way to escape command line params depends in large part on the
        /// particular param parsing routine that is being used by the particular executable you are
        /// running. So a .net executable will be very different from something written in C++.
        /// Just for the hell of it, I tried doing a bunch of testing with a .net app to see if I could safely escape
        /// ANY input and I quickly gave up. So I thought that I'd just try and handle nothing
        /// but dubl-quotes... but even that was a ridiculous pile of horseshit. It's NOT what you would
        /// think - you can't just double up (like using "" for every literal double-quote) and slap
        /// double quotes around every param. That almost works - sometimes - but there are many cases where it doesn't.
        /// It's so inconsistent that it might as well be random.
        /// </remarks>
        /// <param name="pathToPhantomJSEXE">Path to the PhantomJS executable.</param>
        /// <param name="pathToJavascriptFile">The actual javascript file to send to PhantomJS for execution.</param>
        /// <param name="additionalArgs">Additional command line parameters to be sent to PhantomJS (and ultimately into your JS file).</param>
        /// <returns>The combined string output from both STDOUT and STDERR from the PhantomJS executable.</returns>
        public static object ExecutePhantomScript(string pathToPhantomJSEXE, string pathToJavascriptFile, params string[] additionalArgs)
        {
            if (additionalArgs != null && additionalArgs.Length > 0)
            {
                // Disallow parameters that have embedded double quotes.
                // See <remarks> node above in method comments.
                if (additionalArgs.Any(p => p.Contains("\"")))
                {
                    throw new Exception("One or more parameters had embedded double-quotes; this is not allowed.");
                }
            }

            var output = new BlockingCollection<string>();

            using (var p = new Process())
            {
                // Put literal double quotes around each command line param
                var stringArgs = new StringBuilder().Append($"\"{pathToJavascriptFile}\"");
                if (additionalArgs != null)
                {
                    foreach (var a in additionalArgs)
                    {
                        // Put literal double quotes around each command line param
                        stringArgs.Append($" \"{a}\"");
                    }
                }
                p.OutputDataReceived += (e, v) => output.Add($"stdout: {v.Data}");
                p.ErrorDataReceived += (e, v) => output.Add($"stderr: {v.Data}");
                p.StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    Arguments = stringArgs.ToString(),
                    FileName = pathToPhantomJSEXE
                };
                p.Start();
                p.BeginErrorReadLine();
                p.BeginOutputReadLine();
                p.WaitForExit();
            }
            return output.Aggregate(new StringBuilder(), (b, e) => b.Append($"{e}{Environment.NewLine}"), b => b.ToString());
        }
    }
}