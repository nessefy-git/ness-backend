namespace SocialMediaAuthAPI.Models
{
    public class EmailOtp
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string Otp { get; set; }
        public string PasswordHash { get; set; }
        public DateTime ExpiryTime { get; set; }
        public DateTime LastSentTime { get; set; }  // New field
    }



}
