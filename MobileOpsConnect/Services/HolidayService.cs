using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MobileOpsConnect.Services
{
    /// <summary>
    /// Fetches public holidays from the Nager.Date API (completely free, no API key).
    /// Endpoint: https://date.nager.at/api/v3/PublicHolidays/{year}/{countryCode}
    /// </summary>
    public class HolidayService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HolidayService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

        public HolidayService(HttpClient httpClient, ILogger<HolidayService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// Returns a list of public holidays for the given country and year.
        /// Defaults to the Philippines (PH) and the current year.
        /// </summary>
        public async Task<List<PublicHoliday>> GetHolidaysAsync(string countryCode = "PH", int? year = null)
        {
            var normalizedCountry = (countryCode ?? "PH").Trim().ToUpperInvariant();
            var targetYear = year ?? PhilippineTime.Now.Year;
            var cacheKey = $"holidays::{normalizedCountry}::{targetYear}";

            if (_cache.TryGetValue(cacheKey, out List<PublicHoliday>? cachedHolidays) && cachedHolidays != null)
            {
                return cachedHolidays;
            }

            try
            {
                var url = $"https://date.nager.at/api/v3/PublicHolidays/{targetYear}/{normalizedCountry}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var holidays = JsonSerializer.Deserialize<List<PublicHoliday>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var result = holidays ?? new List<PublicHoliday>();
                _cache.Set(cacheKey, result, CacheDuration);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch public holidays from Nager.Date API.");
                return new List<PublicHoliday>();
            }
        }

        /// <summary>
        /// Checks if a specific date falls on a public holiday.
        /// </summary>
        public async Task<PublicHoliday?> GetHolidayOnDateAsync(DateTime date, string countryCode = "PH")
        {
            var holidays = await GetHolidaysAsync(countryCode, date.Year);
            return holidays.FirstOrDefault(h => h.Date == DateOnly.FromDateTime(date));
        }
    }

    public class PublicHoliday
    {
        public DateOnly Date { get; set; }
        public string LocalName { get; set; } = "";
        public string Name { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public bool Fixed { get; set; }
        public bool Global { get; set; }
        public string[]? Types { get; set; }
    }
}
