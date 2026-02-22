using System.Text.Json;

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

        public ExchangeRateService(HttpClient httpClient, ILogger<ExchangeRateService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Returns a dictionary of currency code → rate, relative to the given base currency.
        /// Falls back to an empty dictionary on failure.
        /// </summary>
        public async Task<ExchangeRateResult> GetRatesAsync(string baseCurrency = "PHP")
        {
            try
            {
                var url = $"https://open.er-api.com/v6/latest/{baseCurrency}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.GetProperty("result").GetString() != "success")
                {
                    _logger.LogWarning("Exchange Rate API returned non-success result.");
                    return ExchangeRateResult.Empty(baseCurrency);
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

                return new ExchangeRateResult
                {
                    BaseCurrency = baseCurrency,
                    Rates = rates,
                    LastUpdated = lastUpdate,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch exchange rates from API.");
                return ExchangeRateResult.Empty(baseCurrency);
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
