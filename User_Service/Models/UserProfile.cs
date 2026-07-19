namespace User_Service.Models;

public class UserProfile
{
    public long Id { get; set; }

    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public string? Img { get; set; }
    public DateOnly? BirthDate { get; set; }

    public string? Status { get; set; }
    public DateTime? StatusUpdatedAt { get; set; }
}
