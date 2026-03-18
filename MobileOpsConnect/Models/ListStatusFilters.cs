namespace MobileOpsConnect.Models
{
    public static class ListStatusFilters
    {
        public const string Active = "active";
        public const string Archived = "archived";
        public const string All = "all";

        public static string Normalize(string? status, string defaultValue = Active)
        {
            var value = string.IsNullOrWhiteSpace(status) ? defaultValue : status.Trim();

            if (value.Equals("Active", StringComparison.OrdinalIgnoreCase)) return Active;
            if (value.Equals("Archived", StringComparison.OrdinalIgnoreCase)) return Archived;
            if (value.Equals("All", StringComparison.OrdinalIgnoreCase)) return All;

            return value.ToLowerInvariant();
        }
    }
}
