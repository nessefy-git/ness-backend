
using Microsoft.AspNetCore.Identity;

namespace SocialMediaAuthAPI.Models
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsEmailVerified { get; set; }

    }

}
