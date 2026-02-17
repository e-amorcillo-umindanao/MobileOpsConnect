namespace MobileOpsConnect.Models
{
    public class UserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string CurrentRole { get; set; } = string.Empty;
        public string NewRole { get; set; } = string.Empty;
        public bool IsOwnAccount { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }

    public class EmployeeRecordViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int PendingLeaves { get; set; }
        public int ApprovedLeaves { get; set; }
    }
}
