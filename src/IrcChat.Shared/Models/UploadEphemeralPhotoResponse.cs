namespace IrcChat.Shared.Models;

public record UploadEphemeralPhotoResponse
{
    public required string ImageUrl { get; init; }
    public required string ThumbnailUrl { get; init; }
}