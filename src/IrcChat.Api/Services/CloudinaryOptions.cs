namespace IrcChat.Api.Services;

public class CloudinaryOptions
{
    public static readonly string SectionName = "Cloudinary";

    public required string CloudName { get; set; }

    public required string ApiKey { get; set; }

    public required string ApiSecret { get; set; }
    public string EphemeralFolder { get; set; } = "ircchat-ephemeral";
    public int SignedUrlExpirationHours { get; set; } = 24;
}