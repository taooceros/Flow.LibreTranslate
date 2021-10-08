using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.LibreTranslate
{
    public class LibreTranslate : IAsyncPlugin
    {
        private PluginInitContext _context;
        internal List<Language> SupportedLanguage;

        internal const string LibreTranslateUrl = "https://translate.argosopentech.com";
        internal const string LanguageListUrl = LibreTranslateUrl + "/languages";
        internal const string TranslateUrl = LibreTranslateUrl + "/translate";

        internal const string IconPath = "Images\\translate.png";

        public async Task InitAsync(PluginInitContext context)
        {
            this._context = context;

            await using var stream = await this._context.API.HttpGetStreamAsync(LanguageListUrl);

            SupportedLanguage = await JsonSerializer.DeserializeAsync<List<Language>>(stream);
        }

        private readonly HttpClient _client = new();

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            var searches = query.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var semiQueryCompleted = searches.Length == 0 || SupportedLanguage.Any(x => x.Code == searches[^1]);

            if (searches.Length < 3)
            {
                if (searches.Length == 2 && semiQueryCompleted)
                    return new List<Result>();
                return SupportedLanguage.Select(l => new Result
                    {
                        Title = l.Name,
                        SubTitle = l.Code,
                        Action = _ =>
                        {
                            _context.API.ChangeQuery(
                                $"{_context.CurrentPluginMetadata.ActionKeyword} {(semiQueryCompleted ? query.Search : string.Join(' ', searches[..^1]))}{(searches.Length == 0 ? "" : " ")}{l.Code} "
                            );
                            return false;
                        },
                        IcoPath = IconPath,
                        Score = semiQueryCompleted ? 100 : _context.API.FuzzySearch(searches[^1], l.Name).Score
                    }).Where(r => r.Score > 0)
                    .ToList();
            }

            var from = SupportedLanguage.Find(x => x.Code == searches[0]);
            var to = SupportedLanguage.Find(x => x.Code == searches[1]);

            if (from is null || to is null)
            {
                return new List<Result>
                {
                    new()
                    {
                        Title = "One of the Language code does not exist", IcoPath = IconPath
                    }
                };
            }

            string text = string.Join(' ', searches[2..]);
            var requestContent = new Dictionary<string, string>
            {
                {
                    "q", text
                },
                {
                    "source", from.Code
                },
                {
                    "target", to.Code
                },
                {
                    "api_key", ""
                }
            };


            await Task.Delay(150, token);
            token.ThrowIfCancellationRequested();

            var content = new FormUrlEncodedContent(requestContent);
            using var response = await _client.PostAsync(TranslateUrl, content, token);

            await using var resultStream = await response.Content.ReadAsStreamAsync(token);
            using var result = await JsonDocument.ParseAsync(resultStream, cancellationToken: token);

            return new List<Result>
            {
                new()
                {
                    Title = $"Translated Text: {result.RootElement.GetProperty("translatedText")}",
                    SubTitle = $"{text}: From {from.Name} to {to.Name}",
                    Score = 100,
                    IcoPath = IconPath
                }
            };

        }
    }
}