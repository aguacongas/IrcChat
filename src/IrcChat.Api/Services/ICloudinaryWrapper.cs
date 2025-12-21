using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace IrcChat.Api.Services;

public interface ICloudinaryWrapper
{
    Url UrlImgUp { get; }
    Task<ImageUploadResult> UploadAsync(ImageUploadParams uploadParams, CancellationToken? cancellationToken = null);
    Task<DeletionResult> DestroyAsync(DeletionParams parameters);
    Task<ListResourcesResult> ListResourcesAsync(ListResourcesParams parameters, CancellationToken? cancellationToken = null);

    Task<DelResResult> DeleteResourcesAsync(DelResParams parameters, CancellationToken? cancellationToken = null);
}
