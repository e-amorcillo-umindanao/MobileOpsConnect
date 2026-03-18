using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MobileOpsConnect.Services
{
    /// <summary>
    /// Fetches live exchange rates from the ExchangeRate-API (free tier).
    /// Endpoint: https://open.er-api.com/v6/latest/{baseCurrency}
    /// </summary>
    public class ExchangeRateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ExchangeRateService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        public ExchangeRateService(HttpClient httpClient, ILogger<ExchangeRateService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// Returns a dictionary of currency code → rate, relative to the given base currency.
        /// Falls back to an empty dictionary on failure.
        /// </summary>
        public async Task<ExchangeRateResult> GetRatesAsync(string baseCurrency = "PHP")
        {
            var normalizedBase = (baseCurrency ?? "PHP").Trim().ToUpperInvariant();
            var cacheKey = $"exchange-rates::{normalizedBase}";

            if (_cache.TryGetValue(cacheKey, out ExchangeRateResult? cachedResult) && cachedResult != null)
            {
                return cachedResult;
            }

            try
            {
                var url = $"https://open.er-api.com/v6/latest/{normalizedBase}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("result").GetString() != "success")
                {
                    _logger.LogWarning("Exchange Rate API returned non-success result.");
                    return ExchangeRateResult.Empty(normalizedBase);
                }

                var rates = new Dictionary<string, decimal>();
                foreach (var prop in root.GetProperty("rates").EnumerateObject())
                {
                    if (prop.Value.TryGetDecimal(out var rate))
                    {
                        rates[prop.Name] = rate;
                    }
                }

                var lastUpdate = root.TryGetProperty("time_last_update_utc", out var timeEl)
                    ? timeEl.GetString() ?? "Unknown"
                    : "Unknown";

                var result = new ExchangeRateResult
                {
                    BaseCurrency = normalizedBase,
                    Rates = rates,
                    LastUpdated = lastUpdate,
                    IsSuccess = true
                };

                _cache.Set(cacheKey, result, CacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch exchange rates from API.");
                return ExchangeRateResult.Empty(normalizedBase);
            }
        }
    }

    public class ExchangeRateResult
    {
        public string BaseCurrency { get; set; } = "PHP";
        public Dictionary<string, decimal> Rates { get; set; } = new();
        public string LastUpdated { get; set; } = "Unknown";
        public bool IsSuccess { get; set; }

        public static ExchangeRateResult Empty(string baseCurrency) => new()
        {
            BaseCurrency = baseCurrency,
            Rates = new Dictionary<string, decimal>(),
            IsSuccess = false
        };
    }
}
