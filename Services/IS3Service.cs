namespace SocialMediaAuthAPI.Services
{
    public interface IS3Service
    {
        Task<string> UploadProfileImageAsync(IFormFile file, string userId);
    }

}
