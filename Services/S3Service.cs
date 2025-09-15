using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace SocialMediaAuthAPI.Services
{
    public class S3Service : IS3Service
    {
        private readonly IConfiguration _config;
        private readonly IAmazonS3 _s3Client;

        public S3Service(IConfiguration config)
        {
            _config = config;
            _s3Client = new AmazonS3Client(
                _config["AWS:AccessKey"],
                _config["AWS:SecretKey"],
                RegionEndpoint.GetBySystemName(_config["AWS:Region"])
            );
        }

        public async Task<string> UploadProfileImageAsync(IFormFile file, string userId)
        {
            var extension = Path.GetExtension(file.FileName);
            var key = $"profile-images/{userId}_{Guid.NewGuid()}{extension}";

            using var stream = file.OpenReadStream();

            var putRequest = new PutObjectRequest
            {
                BucketName = _config["AWS:BucketName"],
                Key = key,
                InputStream = stream,
                ContentType = file.ContentType,
                //CannedACL = S3CannedACL.PublicRead
            };

            await _s3Client.PutObjectAsync(putRequest);

            string url = $"https://{_config["AWS:BucketName"]}.s3.amazonaws.com/{key}";
            return url;
        }
    }

}
