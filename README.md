# ArmRateParser

Doesn't really "parse" anything. And it's not truly specific to "ARM rates."

Basically just a wrapper around playwright to make it slightly easier to 
call from Powershell. Also does a bit of preprocessing on the output that was 
needed for 100% of the web sites I needed to process. 

Output is an object with several properties that are each the path to a temp file:

* WebPageImagePath
  * This is a PNG file of the actual rendered web page
* WebPageHtmlPath
  * This is the raw(*) HTML
* AllTagsJsonPath
  * This is a JSON file containing nearly all meaningful tags from the raw HTML.
  * By default, this includes all of the following tags: td, th, span, div, p, h1, h2, h3, h4
  * Note that you can add to this list (more below)
* DataSetXmlPath
  * Each HTML table in the output is turned into a .net DataTable. All datatables are then put into a DataSet.
    This DataSet is then serialised into XML. The caller can deserialise this dataset and query those tables.
  * _NOTE_: if the site does not have any tables at all, this property will be an empty string.

## Calling the "parser"

```powershell
## unfortch, gotta load these assemblies manually
add-type -path '.\ArmRateParser\bin\Release\net8.0\publish\ArmRateParser.dll'
add-type -path '.\ArmRateParser\bin\Release\net8.0\publish\HtmlAgilityPack.dll'
## make the call - be sure to use .GetAwaiter().GetResult() !!
$url = 'https://www.wellsfargo.com/mortgage/cost-of-savings-index/'
$adam = [ArmRateParser.Processor]::ProcessWebSiteAsync($url).GetAwaiter().GetResult()
## dump it out, bruv
$adam 
```

Output:

```
WebPageImagePath : C:\Users\pj\AppData\Local\Temp\tmprue2t2.tmp.png
WebPageHtmlPath  : C:\Users\pj\AppData\Local\Temp\tmpcqvbew.tmp
AllTagsJsonPath  : C:\Users\pj\AppData\Local\Temp\tmphumqdp.tmp
DataSetXmlPath   : C:\Users\pj\AppData\Local\Temp\tmpfu69420.tmp
PlaywrightError  :
```

## Customising the list of "all tags" tags

The `ProcessWebSiteAsync()` method has an optional second parameter of type `IEnumerable<string>`. For ex., below I'm including 
both `<li>` and `<img>` tags.

```powershell
$adam = [ArmRateParser.Processor]::ProcessWebSiteAsync("https://www.wellsfargo.com/mortgage/cost-of-savings-index/", @("li", "img"))).GetAwaiter().GetResult()
```
