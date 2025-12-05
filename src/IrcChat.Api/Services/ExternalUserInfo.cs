namespace IrcChat.Api.Services;

public class ExternalUserInfo
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? AvatarUrl { get; set; }
}