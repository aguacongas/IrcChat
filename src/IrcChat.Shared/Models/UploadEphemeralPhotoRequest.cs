namespace IrcChat.Shared.Models;

public record UploadEphemeralPhotoRequest
{
    public required string ImageBase64 { get; init; }
}
