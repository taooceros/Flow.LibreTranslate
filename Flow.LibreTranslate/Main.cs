using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Flow.Launcher.Plugin;
using System.Text.Json;
using System.Threading;
using System.Net.Http;
using System.Linq;

namespace Flow.Launcher.Plugin.LibreTranslate
{
    public class LibreTranslate : IAsyncPlugin
    {
        private PluginInitContext _context;
        internal List<Language> supportedLanguage;

        internal const string LanguageListUrl = "https://libretranslate.com/languages";
        internal const string TranslatetUrl = "https://libretranslate.com/translate";

        public async Task InitAsync(PluginInitContext context)
        {
            _context = context;

            using var stream = await _context.API.HttpGetStreamAsync(LanguageListUrl);

            supportedLanguage = await JsonSerializer.DeserializeAsync<List<Language>>(stream);
        }

        private HttpClient client = new HttpClient();

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            if (!(query.SecondSearch.Any() && query.ThirdSearch.Any()))
                return new List<Result>();

            string[] searchs = query.Search.Split();

            var from = supportedLanguage.Find(x => x.Code == searchs[0]);
            var to = supportedLanguage.Find(x => x.Code == searchs[1]);

            string text = string.Join(' ', searchs[2..]);
            var requestContent = new Dictionary<string, string>
            {
                {"q",text},
                {"source", from.Code},
                {"target", to.Code},
                {"api_key",""}
            };

            FormUrlEncodedContent content = new FormUrlEncodedContent(requestContent);
            using var response = await client.PostAsync(TranslatetUrl, content, token);

            var result = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

            return new List<Result>{
                new Result{
                    Title=$"Translated Text: {result.RootElement.GetProperty("translatedText")}",
                    SubTitle=$"{text}: From {from.Name} to {to.Name}",
                    Score=100
                }
            };

        }
    }
}