namespace MobileOpsConnect.Services
{
    /// <summary>
    /// Centralized Philippine Standard Time (UTC+8) provider.
    /// Use PhilippineTime.Now everywhere instead of DateTime.UtcNow or DateTime.Now.
    /// </summary>
    public static class PhilippineTime
    {
        private static readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
        public static DateTime Today => Now.Date;
    }
}
