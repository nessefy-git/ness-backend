public class UserProfileDto
{
    public string Name { get; set; }
    public string ContactNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; }
    public string UserType { get; set; }
    public string? RookieType { get; set; } // Student, Job, Others
  

    // Entrepreneur
 
    public string? ProductName { get; set; }
    public string? ProductDescription { get; set; }

    public string? CompanyName { get; set; }
    public string? AboutCompany { get; set; }

    public string? InvestmentInterest { get; set; }
    public string? InterestedFields { get; set; }
}