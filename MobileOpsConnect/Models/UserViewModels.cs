namespace MobileOpsConnect.Models
{
    public class UserViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
    }

    public class EditUserViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string CurrentRole { get; set; }
        public string NewRole { get; set; }
        public bool IsOwnAccount { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public string Id { get; set; }
        public string Email { get; set; }
        public string NewPassword { get; set; }
    }
}
