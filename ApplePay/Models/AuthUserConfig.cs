namespace ApplePay.Models
{
    public class AuthUsersConfig
    {
        public AuthUser Admin { get; set; } = new();
        public AuthUser TestUser { get; set; } = new();
        public AuthUser ExternalUser { get; set; } = new();
    }

    public class AuthUser
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
    }
}
