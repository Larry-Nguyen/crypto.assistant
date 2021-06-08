using CoinGecko.Clients;
using CoinGecko.Interfaces;
using Google.Cloud.Dialogflow.V2;
using Google.Protobuf;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Crypto.Assistant.WebAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WebhookController : ControllerBase
    {
		private static JsonParser _jsonParser;
        private readonly ILogger<WebhookController> _logger;
        private readonly ICoinGeckoClient _coinGeckoClient;

        protected readonly IMemoryCache _cache;
        protected readonly TimeSpan _cacheExpiration;

        public WebhookController(ILogger<WebhookController> logger, IMemoryCache memoryCache)
        {
            _logger = logger;
            _cache = memoryCache;
            _cacheExpiration = TimeSpan.FromDays(1);
            _coinGeckoClient = CoinGeckoClient.Instance;
            _jsonParser = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));
        }

        [HttpPost]
        public async Task<IActionResult> PostAsync()
        {
			WebhookRequest request;
			using (var reader = new StreamReader(Request.Body))
			{
				request = _jsonParser.Parse<WebhookRequest>(reader);
			}

            var random = new Random();
            var response = new WebhookResponse();
            var responseStringBuilder = new StringBuilder();
            var usCulture = CultureInfo.CreateSpecificCulture("en-US");

			var parameters = request.QueryResult.Parameters;
            var queryText = request.QueryResult.QueryText.ToLower();

            string firstName = null, lastName = null, userName = null; double? userID = null;

            //var payloadFields = request.OriginalDetectIntentRequest?.Payload?.Fields;
            //if (payloadFields != null && payloadFields.ContainsKey("data"))
            //{
            //    var dataFields = payloadFields["data"].StructValue?.Fields;
            //    if (dataFields != null && dataFields.ContainsKey("from"))
            //    {
            //        var fromFields = dataFields["from"].StructValue?.Fields;
            //        if (fromFields != null)
            //        {
            //            firstName = fromFields["first_name"]?.StringValue;
            //            lastName = fromFields["last_name"]?.StringValue;
            //            userName = fromFields["username"]?.StringValue;
            //            userID = fromFields["id"]?.NumberValue;
            //        }
            //    }
            //}

            switch (request.QueryResult.Intent.DisplayName)
            {
                case "crypto.assistant.intent.welcome":
                    {
                        if (queryText.Contains("ơi"))
                        {
                            responseStringBuilder.AppendLine("Ơi!");
                        }
                        else if (queryText.Contains("chào") && !string.IsNullOrEmpty(firstName))
                        {
                            responseStringBuilder.AppendLine($"Chào {firstName}");
                        }
                        else if (queryText.Contains("chào"))
                        {
                            responseStringBuilder.AppendLine("Xin chào");
                        }
                        else if (queryText.Contains("hello") && !string.IsNullOrEmpty(firstName))
                        {
                            responseStringBuilder.AppendLine($"Hello {firstName}");
                        }
                        else if (queryText.Contains("hello"))
                        {
                            responseStringBuilder.AppendLine($"Hello!");
                        }
                        else if (queryText.Contains("hi") && !string.IsNullOrEmpty(firstName))
                        {
                            responseStringBuilder.AppendLine($"Hi {firstName}");
                        }
                        else if (queryText.Contains("hi"))
                        {
                            responseStringBuilder.AppendLine($"Hi!");
                        }
                        else
                        {
                            var messages = new[]
                            {
                                "Gì!",
                                "Hả!",
                                "Sao thế!"
                            };
                            var randomIndex = random.Next(0, messages.Length);
                            responseStringBuilder.AppendLine(messages[randomIndex]);
                        }
                    }
                    break;
                case "crypto.assistant.intent.fallback":
                    {
                        var messages = new[]
                            {
                                "Xin lỗi! Mình không hiểu.",
                                "Mình không hiểu!!"
                            };
                        var randomIndex = random.Next(0, messages.Length);
                        responseStringBuilder.AppendLine(messages[randomIndex]);
                    }
                    break;
                case "crypto.assistant.intent.lottery":
                    {
                        var dateTimeValue = parameters.Fields["dateTime"]?.StringValue;
                        DateTimeOffset.TryParse(dateTimeValue, out var timeRequest);
                        var lotteryResults = new Dictionary<string, IEnumerable<string>>();

                        var today = DateTimeOffset.Now.ToOffset(timeRequest.Offset);
                        var lotteryAnnouncementTime = new DateTimeOffset(today.Year, today.Month, today.Day, 18, 30, 0, new TimeSpan(7, 0, 0));

                        if (timeRequest.Date == today.Date && today < lotteryAnnouncementTime)
                        {
                            var messages = new[]
                            {
                                "Chưa tới giờ công bố kết quả nha!",
                                "Đợi sau 06h30 tối nhé!",
                                $"Bây giờ mới {today:hh:mm}. Đợi tới 06h30 tối nhé!"
                            };
                            var randomIndex = random.Next(0, messages.Length);
                            responseStringBuilder.AppendLine(messages[randomIndex]);
                        }
                        else if (timeRequest.Date == today.Date.AddDays(1))
                        {
                            var messages = new[]
                            {
                                $"Ngày mai thì để mai hỏi nhé!",
                                "Đang đùa thôi đúng không?"
                            };
                            var randomIndex = random.Next(0, messages.Length);
                            responseStringBuilder.AppendLine(messages[randomIndex]);
                        }
                        else if (timeRequest.Date > today.Date)
                        {
                            var messages = new[]
                            {
                                $"Nay mới {today:dd/MM}, đợi tới ngày đó nhé!",
                                $"Nay mới {today:dd/MM}!",
                                $"Nay mới {today:dd/MM}, đào kết quả xs ở đâu ra đây!",
                            };
                            var randomIndex = random.Next(0, messages.Length);
                            responseStringBuilder.AppendLine(messages[randomIndex]);
                        }
                        else
                        {
                            try
                            {
                                lotteryResults = await QueryLottery(timeRequest);

                                var messages = new[]
                                {
                                    $"Kết quả XSMB {timeRequest:dd/MM/yyyy}:",
                                    $"KQ XSMB {timeRequest:dd/MM/yyyy}:",
                                };
                                var randomIndex = random.Next(0, messages.Length);
                                responseStringBuilder.AppendLine(messages[randomIndex]);
                            }
                            catch (Exception)
                            {
                                responseStringBuilder.AppendLine("Có lỗi xảy ra, không thể lấy kết quả của ngày này, thử lại sau nhé!");
                            }

                            foreach (var lotteryResult in lotteryResults)
                            {
                                responseStringBuilder.AppendLine(lotteryResult.Key + ": " + string.Join("; ", lotteryResult.Value));
                            }
                        }
                    }
                    break;
                case "crypto.assistant.intent.timequestion":
                    {
                        var today = DateTimeOffset.Now.ToOffset(new TimeSpan(7, 0, 0));
                        var messages = new[]
                        {
                            $"Bây giờ là {today:HH:mm dd/MM/yyyy}",
                            $"{today:HH:mm dd/MM/yyyy}"
                        };
                        var randomIndex = random.Next(0, messages.Length);
                        responseStringBuilder.AppendLine(messages[randomIndex]);
                    }
                    break;
                case "crypto.assistant.intent.cryptocurrencyprice":
                    {
                        var cryptocurrency = parameters.Fields["cryptocurrency"]?.StringValue;
                        var coinData = await _coinGeckoClient.CoinsClient.GetAllCoinDataWithId(cryptocurrency, "false", false, true, false, false, false);
                        var currentPrice = coinData.MarketData.CurrentPrice.ContainsKey("usd") ? coinData.MarketData.CurrentPrice["usd"] : null;
                        var coinSymbol = coinData.Symbol.ToUpper(usCulture);

                        var realTimePrice = await QueryRealtimeCryptoPrice(coinSymbol);
                        if (realTimePrice != null && realTimePrice > 0)
                        {
                            currentPrice = realTimePrice;
                        }

                        if (!string.IsNullOrEmpty(userName))
                        {
                            responseStringBuilder.AppendLine($"@{userName}");
                        }
                        else if (!string.IsNullOrEmpty(firstName))
                        {
                            responseStringBuilder.AppendLine($"@{firstName} {lastName}");
                        }

                        responseStringBuilder.AppendLine($"{coinData.Name}({coinSymbol}): {(currentPrice.HasValue ? currentPrice.Value.ToStringPrice(usCulture) : "$N/A")}");
                    }
                    break;
                case "crypto.assistant.intent.cryptocurrencymarket":
                    {
                        var cryptocurrency = parameters.Fields["cryptocurrency"]?.StringValue;
                        var coinData = await _coinGeckoClient.CoinsClient.GetAllCoinDataWithId(cryptocurrency, "false", true, false, false, false, false);
                        var coinSymbol = coinData.Symbol.ToUpper(usCulture);

                        if (!string.IsNullOrEmpty(userName))
                        {
                            responseStringBuilder.AppendLine($"@{userName}");
                        }
                        else if (!string.IsNullOrEmpty(firstName))
                        {
                            responseStringBuilder.AppendLine($"@{firstName} {lastName}");
                        }

                        if (coinData.Tickers.Any())
                        {
                            responseStringBuilder.AppendLine($"Có thể giao dịch {coinSymbol} trên các market sau: {string.Join(", ", coinData.Tickers.Select(x => x.Market.Name).Distinct())}");
                        }
                        else
                        {
                            responseStringBuilder.AppendLine($"Hiện không có thông tin market cho đồng này");
                        }
                    }
                    break;
                case "crypto.assistant.intent.cryptocurrencyinfo":
                    {
                        var cryptocurrency = parameters.Fields["cryptocurrency"]?.StringValue;
                        var coinData = await _coinGeckoClient.CoinsClient.GetAllCoinDataWithId(cryptocurrency, "false", false, true, false, false, true);
                        var currentPrice = coinData.MarketData.CurrentPrice.ContainsKey("usd") ? coinData.MarketData.CurrentPrice["usd"] : null;
                        var coinSymbol = coinData.Symbol.ToUpper(usCulture);

                        var realTimePrice = await QueryRealtimeCryptoPrice(coinSymbol);
                        if (realTimePrice != null && realTimePrice > 0)
                        {
                            currentPrice = realTimePrice;
                        }

                        if (!string.IsNullOrEmpty(userName))
                        {
                            responseStringBuilder.AppendLine($"@{userName}");
                        }
                        else if (!string.IsNullOrEmpty(firstName))
                        {
                            responseStringBuilder.AppendLine($"@{firstName} {lastName}");
                        }

                        responseStringBuilder.AppendLine($"{coinData.Name}({coinSymbol}): {(currentPrice.HasValue ? currentPrice.Value.ToStringPrice(usCulture) : "$N/A")}");
                        if (coinData.MarketData.PriceChangePercentage24H.HasValue)
                        {
                            var isIncreased = coinData.MarketData.PriceChangePercentage24H.Value > 0;
                            responseStringBuilder.AppendLine($"- Biến động giá trong 24h: {(isIncreased ? "+" : "")}{coinData.MarketData.PriceChangePercentage24H.Value}%");
                        }
                        if (coinData.MarketData.High24H.ContainsKey("usd") && coinData.MarketData.High24H["usd"].HasValue)
                        {
                            responseStringBuilder.AppendLine($"- Giá cao nhất trong 24h: {coinData.MarketData.High24H["usd"].Value.ToStringPrice(usCulture)}");
                        }
                        if (coinData.MarketData.Low24H.ContainsKey("usd") && coinData.MarketData.Low24H["usd"].HasValue)
                        {
                            responseStringBuilder.AppendLine($"- Giá thấp nhất trong 24h: {coinData.MarketData.Low24H["usd"].Value.ToStringPrice(usCulture)}");
                        }
                        if (!string.IsNullOrEmpty(coinData.MarketData.CirculatingSupply))
                        {
                            var isParseSuccess = decimal.TryParse(coinData.MarketData.CirculatingSupply, out var circulatingSupply);
                            if (isParseSuccess && circulatingSupply > 0)
                            {
                                responseStringBuilder.AppendLine($"- Lượng cung lưu hành: {circulatingSupply:N0} {coinSymbol}");
                            }
                        }
                        if (coinData.MarketData.TotalSupply.HasValue && coinData.MarketData.TotalSupply.Value > 0)
                        {
                            responseStringBuilder.AppendLine($"- Tổng cung: {coinData.MarketData.TotalSupply.Value:N0} {coinSymbol}");
                        }
                        if (coinData.MarketData.MarketCap.ContainsKey("usd") && coinData.MarketData.MarketCap["usd"].HasValue && coinData.MarketData.MarketCap["usd"].Value > 0)
                        {
                            responseStringBuilder.AppendLine($"- Vốn hóa thị trường: {coinData.MarketData.MarketCap["usd"].Value.ToStringPrice(usCulture)}");
                        }
                        if (coinData.MarketData.MarketCapRank.HasValue)
                        {
                            responseStringBuilder.AppendLine($"- Xếp hạng: {coinData.MarketData.MarketCapRank.Value}");
                        }
                        if (coinData.Links.Homepage.Any(x => !string.IsNullOrEmpty(x)))
                        {
                            responseStringBuilder.AppendLine($"- Web: {coinData.Links.Homepage.First(x => !string.IsNullOrEmpty(x))}");
                        }
                        if (coinData.GenesisDate != null)
                        {
                            responseStringBuilder.AppendLine($"- Ngày phát hành: {coinData.GenesisDate.Value.ToOffset(new TimeSpan(7, 0, 0)):dd/MM/yyyy}");
                        }
                        if (coinData.Categories.Any())
                        {
                            responseStringBuilder.AppendLine($"- Thể loại: {string.Join(", ", coinData.Categories)}");
                        }
                        if (coinData.Platforms?.Any(x => !string.IsNullOrEmpty(x.Key)) == true)
                        {
                            responseStringBuilder.AppendLine("- Hợp đồng:");
                            var index = 1;
                            foreach (var platform in coinData.Platforms)
                            {
                                if (string.IsNullOrEmpty(platform.Key)) continue;
                                responseStringBuilder.AppendLine($"  {index++}. {platform.Key}: {platform.Value}");
                            }
                        }
                    }
                    break;
                default:
                    break;
            }

            if (!response.FulfillmentMessages.Any())
            {
                response.FulfillmentText = responseStringBuilder.ToString();
            }

			return Ok(response);
		}

        private async Task<Dictionary<string, IEnumerable<string>>> QueryLottery(DateTimeOffset dateTime)
		{
			var cachekey = $"QueryLottery_{dateTime:ddMMyyy}";

            if (!_cache.TryGetValue(cachekey, out Dictionary<string, IEnumerable<string>> cacheEntry))
            {
                var htmlText = string.Empty;

                using (var client = new HttpClient())
                {
                    var url = $"https://vuaketqua.com/kqxs-theo-mien-xsmb-ngay-{dateTime:dd-MM-yyyy}";
                    using var response = await client.GetAsync(url);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        htmlText = await response.Content.ReadAsStringAsync();
                    }
                }

                if (!string.IsNullOrEmpty(htmlText))
                {
                    var htmlDocument = new HtmlDocument
                    {
                        OptionFixNestedTags = true,
                        OptionEmptyCollection = true
                    };

                    htmlDocument.LoadHtml(htmlText);

                    if (htmlDocument.DocumentNode != null)
                    {
                        var path = $"//div[contains(@class, 'kqxs-{dateTime:ddMMyyyy}-xsmb')]//table";
                        var tableNode = htmlDocument.DocumentNode.SelectSingleNode(path);
                        var cellNodes = tableNode.SelectNodes(".//tbody//tr//td");
                        var cellValues = cellNodes.Select(x => x.InnerText).ToArray();

                        var rowByPrize = new List<List<string>>();
                        var prizeInfo = new List<string>();

                        for (var i = 0; i < cellValues.Length; i++)
                        {
                            prizeInfo.Add(cellValues[i]);
                            var nextCellIsTextOrUndefined = i + 1 == cellValues.Length || !int.TryParse(cellValues[i + 1], out var _);
                            if (nextCellIsTextOrUndefined)
                            {
                                rowByPrize.Add(prizeInfo);
                                prizeInfo = new List<string>();
                            }
                        }

                        cacheEntry = rowByPrize.ToDictionary(x => x.First(), x => x.Where(y => !y.Equals(x.First())));

                        if (cacheEntry != null)
                        {
                            var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(DateTimeOffset.UtcNow.Add(_cacheExpiration));
                            _cache.Set(cachekey, cacheEntry, cacheEntryOptions);
                        }
                    }
                }
            }

            return cacheEntry;
		}

        private async Task<decimal?> QueryRealtimeCryptoPrice(string cryptocurrencySymbol, string cryptocurrencySymbolsToConvertInto = "USD")
        {
            var priceText = $"{{ \"{cryptocurrencySymbolsToConvertInto}\":0 }}";

            using (var client = new HttpClient())
            {
                var apiKey = "100f4c6454794b9050b3ccfbd92f2298c152abfd713bd96727c5ae8036c2a8b5";
                var url = $"https://min-api.cryptocompare.com/data/price?fsym={cryptocurrencySymbol}&tsyms={cryptocurrencySymbolsToConvertInto}&api_key={apiKey}";
                using var response = await client.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    priceText = await response.Content.ReadAsStringAsync();
                }
            }

            dynamic deserializedObject = JsonConvert.DeserializeObject(priceText);
            var price = deserializedObject[cryptocurrencySymbolsToConvertInto];

            return price;
        }
    }
}
