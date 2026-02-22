using System.Text.Json;

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

        public HolidayService(HttpClient httpClient, ILogger<HolidayService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Returns a list of public holidays for the given country and year.
        /// Defaults to the Philippines (PH) and the current year.
        /// </summary>
        public async Task<List<PublicHoliday>> GetHolidaysAsync(string countryCode = "PH", int? year = null)
        {
            try
            {
                var targetYear = year ?? DateTime.Now.Year;
                var url = $"https://date.nager.at/api/v3/PublicHolidays/{targetYear}/{countryCode}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var holidays = JsonSerializer.Deserialize<List<PublicHoliday>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return holidays ?? new List<PublicHoliday>();
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
