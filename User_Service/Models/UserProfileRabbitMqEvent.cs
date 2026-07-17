namespace User_Service.Models
{
    public record UserProfileRabbitMqEvent
    (
        long Id,
        string Username,
        string DisplayName,
        string? Img

    );
}
