using SocialMediaAuthAPI.Models;

namespace SocialMediaAuthAPI.Services
{
    public interface IJwtTokenService
    {
        string GenerateToken(ApplicationUser user);
    }

}
