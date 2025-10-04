using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMediaAuthAPI.Data;
using SocialMediaAuthAPI.DTOs;
using SocialMediaAuthAPI.Models;
using SocialMediaAuthAPI.Services;

namespace SocialMediaAuthAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly AppDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IS3Service _s3Service;

        public AuthController(UserManager<ApplicationUser> userManager,
                              SignInManager<ApplicationUser> signInManager,
                              IEmailService emailService,
                              AppDbContext context, IJwtTokenService jwtTokenService,IS3Service s3Service
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _context = context;
            _jwtTokenService = jwtTokenService;
            _s3Service = s3Service;
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequestDto dto)
        {
            var existingOtp = await _context.EmailOtps
                .FirstOrDefaultAsync(e => e.Email == dto.Email);
            if (existingOtp != null)
            {
                _context.EmailOtps.Remove(existingOtp);
                await _context.SaveChangesAsync();
            }

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return BadRequest("Email is already registered.");

            // Generate OTP
            var otp = new Random().Next(100000, 999999).ToString();
            var passwordHash = _userManager.PasswordHasher.HashPassword(null, dto.Password);

            var otpEntry = new EmailOtp
            {
                Email = dto.Email,
                Otp = otp,
                PasswordHash = passwordHash,
                ExpiryTime = DateTime.UtcNow.AddMinutes(5)
            };

            _context.EmailOtps.Add(otpEntry);
            await _context.SaveChangesAsync();

            await _emailService.SendEmailAsync(dto.Email, "OTP Verification", $"Your OTP is {otp}");

            return Ok("OTP sent to your email. Please verify to complete registration.");
        }


        //[HttpPost("verify-otp")]
        //public async Task<IActionResult> VerifyOtp(VerifyOtpDto dto)
        //{
        //    var otpRecord = await _context.EmailOtps
        //        .FirstOrDefaultAsync(x => x.Email == dto.Email && x.Otp == dto.Otp && x.ExpiryTime > DateTime.UtcNow);

        //    if (otpRecord == null) return BadRequest("Invalid or expired OTP.");

        //    // Check if user already exists (should not)
        //    var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        //    if (existingUser != null) return BadRequest("User already verified.");

        //    var user = new ApplicationUser
        //    {
        //        Email = dto.Email,
        //        UserName = dto.Email,
        //        IsEmailVerified = true
        //    };

        //    user.PasswordHash = otpRecord.PasswordHash;

        //    var result = await _userManager.CreateAsync(user);
        //    if (!result.Succeeded) return BadRequest(result.Errors);

        //    _context.EmailOtps.Remove(otpRecord);
        //    await _context.SaveChangesAsync();

        //    return Ok("Email verified and account successfully created.");
        //}

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var otpEntry = await _context.EmailOtps
                .FirstOrDefaultAsync(o => o.Email == dto.Email && o.Otp == dto.Otp);

            if (otpEntry == null || otpEntry.ExpiryTime < DateTime.UtcNow)
                return BadRequest("Invalid or expired OTP");

            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return BadRequest("User already registered");

            // Create user after successful OTP verification
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                IsEmailVerified = true,
                ProfileCompleted = false // default
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // ✅ Generate JWT token
            var token = _jwtTokenService.GenerateToken(user);

            // ✅ Return token + ProfileCompleted status
            return Ok(new
            {
                Token = token,
                Email = user.Email,
                UserId = user.Id,
                ProfileCompleted = user.ProfileCompleted
            });
        }

        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] string email)
        {
            var otpRecord = await _context.EmailOtps.FirstOrDefaultAsync(e => e.Email == email);

            if (otpRecord == null)
                return BadRequest("No pending registration found for this email.");

            // Check cooldown
            var cooldownSeconds = 60;
            var timeSinceLastSent = DateTime.UtcNow - otpRecord.LastSentTime;
            if (timeSinceLastSent.TotalSeconds < cooldownSeconds)
            {
                var waitTime = cooldownSeconds - (int)timeSinceLastSent.TotalSeconds;
                return BadRequest($"Please wait {waitTime} seconds before requesting another OTP.");
            }

            // Generate new OTP
            var newOtp = new Random().Next(100000, 999999).ToString();
            otpRecord.Otp = newOtp;
            otpRecord.ExpiryTime = DateTime.UtcNow.AddMinutes(5);
            otpRecord.LastSentTime = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _emailService.SendEmailAsync(email, "Your OTP Code", $"Your new OTP is: {newOtp}");

            return Ok("OTP resent to your email.");
        }



        //[HttpPost("login")]
        //public async Task<IActionResult> Login(LoginRequestDto dto)
        //{
        //    var user = await _userManager.FindByEmailAsync(dto.Email);
        //    if (user == null || !user.IsEmailVerified)
        //        return Unauthorized("User not found or email not verified.");

        //    var result = await _signInManager.PasswordSignInAsync(user, dto.Password, false, false);

        //    return result.Succeeded ? Ok("Login successful") : Unauthorized("Invalid credentials.");
        //}

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequestDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
                return Unauthorized("Invalid credentials");

            if (!user.IsEmailVerified)
                return BadRequest("Email not verified.");

            var token = _jwtTokenService.GenerateToken(user);

            return Ok(new
            {
                Token = token,
                Email = user.Email,
                UserId = user.Id,
                ProfileCompleted = user.ProfileCompleted
            });
        }



        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null )
                return BadRequest("Invalid request");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // IMPORTANT: URL Encode token
            var encodedToken = WebUtility.UrlEncode(token);

            var resetLink = $"https://localhost:7065/reset-password?email={dto.Email}&token={encodedToken}";

            await _emailService.SendEmailAsync(dto.Email, "Reset Your Password",
                $"Click the link to reset your password: <a href='{resetLink}'>Reset Password</a>");

            return Ok("Password reset link sent to your email.");
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return BadRequest("User not found.");

            var decodedToken = WebUtility.UrlDecode(dto.Token);

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Password reset successfully.");
        }

        //[Authorize]
        //[HttpPost("complete-profile")]
        //public async Task<IActionResult> CompleteUserProfile([FromBody] UserProfileDto dto)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var user = await _userManager.FindByIdAsync(userId);

        //    if (user == null) return Unauthorized();

        //    user.Name = dto.Name;
        //    user.ContactNumber = dto.ContactNumber;
        //    user.DateOfBirth = dto.DateOfBirth;
        //    user.Gender = dto.Gender;
        //    user.UserType = dto.UserType;

        //    switch (dto.UserType.ToLower())
        //    {
        //        case "entrepreneur":
        //            user.ProductName = dto.ProductName;
        //            user.ProductDescription = dto.ProductDescription;
        //            break;
        //        case "company":
        //            user.CompanyName = dto.CompanyName;
        //            user.AboutCompany = dto.AboutCompany;
        //            break;
        //        case "investor":
        //            user.InvestmentInterest = dto.InvestmentInterest;
        //            break;
        //        case "rookie":
        //            user.InterestedFields = dto.InterestedFields;
        //            break;
        //    }

        //    user.ProfileCompleted = true;
        //    await _userManager.UpdateAsync(user);

        //    return Ok("Profile completed.");
        //}

        //[Authorize]
        //[HttpPost("complete-profile")]
        //public async Task<IActionResult> CompleteUserProfile([FromBody] UserProfileDto dto)
        //{
        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var user = await _userManager.FindByIdAsync(userId);

        //    if (user == null) return Unauthorized();

        //    user.Name = dto.Name;
        //    user.ContactNumber = dto.ContactNumber;
        //    user.DateOfBirth = dto.DateOfBirth;
        //    user.Gender = dto.Gender;
        //    user.UserType = dto.UserType;

        //    switch (dto.UserType.ToLower())
        //    {
        //        case "entrepreneur":
        //            user.ProductName = dto.ProductName;
        //            user.ProductDescription = dto.ProductDescription;
        //            break;

        //        case "company":
        //            user.CompanyName = dto.CompanyName;
        //            user.AboutCompany = dto.AboutCompany;
        //            break;

        //        case "investor":
        //            user.InvestmentInterest = dto.InvestmentInterest;
        //            break;

        //        case "rookie":
        //            user.RookieType = dto.RookieType; // NEW
        //            user.InterestedFields = dto.InterestedFields;
        //            break;
        //    }

        //    user.ProfileCompleted = true;
        //    await _userManager.UpdateAsync(user);

        //    return Ok("Profile completed.");
        //}



        [Authorize]
        [HttpPost("complete-profile")]
        public async Task<IActionResult> CompleteUserProfile([FromForm] UserProfileDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null) return Unauthorized();

            // ✅ Upload profile photo to S3 if provided
            if (dto.ProfilePhoto != null && dto.ProfilePhoto.Length > 0)
            {
                var photoUrl = await _s3Service.UploadProfileImageAsync(dto.ProfilePhoto, userId);
                user.ProfilePhotoUrl = photoUrl;
            }

            // ✅ Update other fields
            user.Name = dto.Name;
            user.ContactNumber = dto.ContactNumber;
            user.DateOfBirth = dto.DateOfBirth;
            user.Gender = dto.Gender;
            user.UserType = dto.UserType;

            switch (dto.UserType.ToLower())
            {
                case "entrepreneur":
                    user.ProductName = dto.ProductName;
                    user.ProductDescription = dto.ProductDescription;
                    break;

                case "company":
                    user.CompanyName = dto.CompanyName;
                    user.AboutCompany = dto.AboutCompany;
                    break;

                case "investor":
                    user.InvestmentInterest = dto.InvestmentInterest;
                    break;

                case "rookie":
                    user.RookieType = dto.RookieType;
                    user.InterestedFields = dto.InterestedFields;
                    break;
            }

            user.ProfileCompleted = true;
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "Profile completed.", profilePhotoUrl = user.ProfilePhotoUrl });
        }

        [Authorize]
        [HttpGet("get-profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return Unauthorized(new { message = "User not found." });

            var userProfile = new
            {
                user.Name,
                user.Email,
                user.ContactNumber,
                user.DateOfBirth,
                user.Gender,
                user.UserType,
                user.ProfilePhotoUrl,
                user.ProductName,
                user.ProductDescription,
                user.CompanyName,
                user.AboutCompany,
                user.InvestmentInterest,
                user.RookieType,
                user.InterestedFields,
                user.ProfileCompleted
            };

            return Ok(userProfile);
        }



    }

}
