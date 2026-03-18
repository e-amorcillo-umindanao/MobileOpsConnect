namespace MobileOpsConnect.Models
{
    public static class NotificationStatusFilters
    {
        public const string Unread = "unread";
        public const string All = "all";

        public static string Normalize(string? status)
        {
            var value = string.IsNullOrWhiteSpace(status) ? Unread : status.Trim();
            if (value.Equals("Unread", StringComparison.OrdinalIgnoreCase)) return Unread;
            if (value.Equals("All", StringComparison.OrdinalIgnoreCase)) return All;
            return value.ToLowerInvariant();
        }
    }
}
