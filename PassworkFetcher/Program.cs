using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace PassworkFetcher
{
    class Program
    {
        const string BaseUrl = "http://cpnettest.azurewebsites.net";
        const string PwdListUrl = BaseUrl + "/Passwords";


        static async Task Main(string[] args)
        {
            try
            {
                //var list = await GetHtmlParseManually();
                var list = await GetHtmlParseAgility();

                foreach (var pwd in list)
                {
                    Console.WriteLine($"{pwd.User} - {pwd.Password}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }


        #region Manual HTML parsing

        private static async Task<IReadOnlyCollection<UserPasswordPair>> GetHtmlParseManually()
        {
            var text = await GetText(PwdListUrl);
            var linkList = ParseListHtml(text);

            var res = new List<UserPasswordPair>();

            foreach (var link in linkList)
            {
                var pwdHtml = await GetText(BaseUrl + link);
                res.Add(ParsePasswordHtml(pwdHtml));
            }

            return res;
        }


        private static async Task<string> GetText(string url)
        {
            using var client = new HttpClient();

            var result = await client.GetAsync(url);

            if (! result.IsSuccessStatusCode)
                throw new ArgumentException($"Incorrect status code: {result.StatusCode}.");

            return await result.Content.ReadAsStringAsync();
        }


        private static IReadOnlyCollection<string> ParseListHtml(string html)
        {
            var rx = new Regex("<a\\s+href=\"(?<link> /.+?)\">", RegexOptions.IgnorePatternWhitespace);

            return rx.Matches(html)
                .Select(m => m.Groups["link"].Value)
                .ToList();
        }


        public static UserPasswordPair ParsePasswordHtml(string html)
        {
            var trRx = new Regex("<tr.*?>(?<cell> .+?)</tr>", RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
            var tdRx = new Regex("<th.*?>(?<header> .+?)</th>.*?<td.*?>(?<data> .+?)</td>", RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);

            var rows = trRx.Matches(html).Cast<Match>()
                .Select(tr => tdRx.Match(tr.Groups["cell"].Value))
                .Where(m => m.Success)
                .Select(m => (m.Groups["header"].Value, m.Groups["data"].Value))
                .ToList();

            return new UserPasswordPair
            {
                User = rows.FirstOrDefault(r => r.Item1 == "User name").Item2,
                Password = rows.FirstOrDefault(r => r.Item1 == "Password").Item2
            };
        }

        #endregion


        #region HTML Agility parsing

        private static async Task<IReadOnlyCollection<UserPasswordPair>> GetHtmlParseAgility()
        {
            var web = new HtmlWeb();
            var listPage = await web.LoadFromWebAsync(PwdListUrl);

            var linkList = listPage.DocumentNode
                .SelectNodes("//a[@href]")
                .Select(a => a.Attributes["href"].Value)
                .Where(lnk => lnk.Length > 1 && lnk.StartsWith("/"))
                .ToList();

            var res = new List<UserPasswordPair>();
            foreach (var link in linkList)
            {
                var pwdPage = await web.LoadFromWebAsync(BaseUrl + link);
                var rows = pwdPage.DocumentNode
                    .SelectNodes("//tr")
                    .Select(tr => (tr.SelectSingleNode("th").InnerText, tr.SelectSingleNode("td").InnerText));
                
                res.Add(new UserPasswordPair
                {
                    User = rows.FirstOrDefault(r => r.Item1 == "User name").Item2,
                    Password = rows.FirstOrDefault(r => r.Item1 == "Password").Item2
                });
            }

            return res;
        }

        #endregion
    }
}
