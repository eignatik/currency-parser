using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Linq;
using System.Text;
using System.IO;

namespace consoleproject {
   class Program {
    
    // Regular expressions pattern for matching all files with rates.
    private static String xmlFilesPattern = @"c\d+z\d+\.xml";
    // Regular expressions pattern for matching all dates from files directory.
    private static String datePattern = @"\d+-\d+-\d+";
    // Regular expression object to be used for matching the corresponding pattern. See xmlFilesPattern(String)
    private static Regex fileNameRegex = new Regex(xmlFilesPattern);
    // Regular expression object to be used for matching the corresponding pattern. See datePattern(String).
    private static Regex dateRegex = new Regex(datePattern);
    // Base URL that is a root for XML files to be parsed.
    private static String baseUrlForXml = @"http://www.nbp.pl/kursy/xml/";
    
    // Relative xPath for currency code.
    private static String CURRENCY_CODE_ATTRIBUTE = @"kod_waluty";
    // Relative xPath for buying rate.
    private static String BUYING_RATE_ATTRIBUTE = @"kurs_kupna";
    // Relative xPath for selling rate.
    private static String SELLING_RATE_ATTRIBUTE = @"kurs_sprzedazy";

    //TODO: pass start and end date as arguments or enter from console.
    //TODO: pass curency code from console/arguments (allowed codes are USD, EUR, CHF, GBP, but in fact it already works with everything).
    //TODO: decompose this class to small ones (otherwice it's just a God class with lots of constants and methods).
    // Note: nbp.pl web site doesn't have a really good web server. Because of this issue big selections take longer time, 
    // and some of the data loss due to connectivity issues. Then more selection, then less data consistency and longer execution.
    public static void Main(string[] args) {
        // DEFAULTS: 
        var currencyCode = @"USD";
        DateTime startDate = DateTime.Parse("2015-12-28");
        DateTime endDate = DateTime.Parse("2015-12-28");

        analyzeCurrency(startDate, endDate, currencyCode);
    }

    static void analyzeCurrency(DateTime startDate, DateTime endDate, String currencyCode) {
        List<String> filteredFiles = collectFilesForDates(startDate, endDate);
        List<Rate> allRates = new List<Rate>();
        foreach (var file in filteredFiles) {
            allRates.AddRange(parseRates(baseUrlForXml + file, currencyCode));
        }
        RateDetails rateDetails = produceRateDetails(allRates, currencyCode);
        displayResults(rateDetails);
    }

    static List<String> collectFilesForDates(DateTime startDate, DateTime endDate) {
        HashSet<String> files = new HashSet<String>();
        List<String> dates = new List<String>();
        using(var client = new WebClient()) {
            var page = client.DownloadString(@"http://www.nbp.pl/kursy/xml/dir.aspx?tt=C");
            foreach (Match match in fileNameRegex.Matches(page)) {
                files.Add(match.Value);
            }
            foreach (Match match in dateRegex.Matches(page)) {
                dates.Add(match.Value);
            }
        }
        var filesList = files.ToList();
        List<String> filteredFiles = new List<String>();
        for (var i = 0; i < filesList.Count; i++) {
            DateTime dateTime = DateTime.Parse(dates[i]);
            if (dateTime.CompareTo(startDate) >= 0 && dateTime.CompareTo(endDate) <= 0) {
                filteredFiles.Add(filesList[i]);
            }
        }
        return filteredFiles;
    }

    private static List<Rate> parseRates(String xmlFileUrl, String currencyCode) {
        List<Rate> rates = new List<Rate>();
        var client = new WebClient();
        try {
            var page = client.DownloadData(xmlFileUrl);
            var xml = Encoding.GetEncoding("ISO-8859-1").GetString(page);
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList nodes = doc.SelectNodes("tabela_kursow/pozycja");
            if (nodes != null) {
                foreach (XmlNode node in nodes) {
                    if (!currencyCode.Equals(node.SelectSingleNode(CURRENCY_CODE_ATTRIBUTE).InnerText)) {
                        continue;
                    }
                    var rate = new Rate() {
                                BuyValue = Double.Parse(node.SelectSingleNode(BUYING_RATE_ATTRIBUTE).InnerText, NumberStyles.Any, new CultureInfo("en-Us")),
                                SellValue = Double.Parse(node.SelectSingleNode(SELLING_RATE_ATTRIBUTE).InnerText, NumberStyles.Any, new CultureInfo("en-Us")),
                                CurrencyCode = node.SelectSingleNode(CURRENCY_CODE_ATTRIBUTE).InnerText
                            };
                    rates.Add(rate);
                }
            } else {
                Console.WriteLine("No positons were found");
            }
        } catch (WebException e) {
            Console.WriteLine(e.Message);
        }
        return rates;
    }

    private static double calculateStandardDeviation(IEnumerable<double> rates, double average) {
        double delta = rates.Select(rate => Math.Pow(rate / average, 2)).Average();
        return Math.Sqrt(delta);
    }

    private static void displayResults(RateDetails rateDetails) {
        Console.WriteLine("Currency code {0}", rateDetails.CurrencyCode);
        printDetals( "Selling", rateDetails.Maxima.SellValue, rateDetails.Minima.SellValue, rateDetails.Average.SellValue, rateDetails.Deviation.SellValue);
        printDetals( "Buying", rateDetails.Maxima.BuyValue, rateDetails.Minima.BuyValue, rateDetails.Average.BuyValue, rateDetails.Deviation.BuyValue);
    }
    private static RateDetails produceRateDetails(List<Rate> allRates, String currencyCode) {
        IEnumerable<double> buyValues = allRates.Select(rate => rate.BuyValue);
        IEnumerable<double> sellValues = allRates.Select(rate => rate.SellValue);
        double buyValuesAvg = buyValues.Average();
        double sellValuesAvg = sellValues.Average();
        double buyValuesDeviation = calculateStandardDeviation(buyValues, buyValuesAvg);
        double sellValuesDeviation = calculateStandardDeviation(sellValues, sellValuesAvg);
        return new RateDetails() {
            CurrencyCode = currencyCode,
            Average = new RateNumber(buyValuesAvg, sellValuesAvg),
            Maxima = new RateNumber(buyValues.Max(), sellValues.Max()),
            Minima = new RateNumber(buyValues.Min(), sellValues.Min()),
            Deviation = new RateNumber(buyValuesDeviation, sellValuesDeviation)
        };
    }

    private static void printDetals(String type, double max, double min, double avg, double deviation) {
        Console.WriteLine("{0} [max={1}, min={2}, average={3}, dev={4}]", type, max, min, avg, deviation);
    }

}
/** 
Data class that represents rate with its currency and value.
*/
public class Rate {
    public string CurrencyCode { get; set; }
    public double SellValue { get; set; }

    public double BuyValue { get; set; }
}

public class RateDetails {
    public String CurrencyCode { get; set; }
    public RateNumber Average { get; set; }
    public RateNumber Deviation { get; set; }
    public RateNumber Maxima { get; set; }
    public RateNumber Minima { get; set; }

}

public class RateNumber {

    public RateNumber(double BuyValue, double SellValue) {
        this.BuyValue = BuyValue;
        this.SellValue = SellValue;
    }
    public double BuyValue { get; set; }
    public double SellValue { get; set; }
}
    
}
