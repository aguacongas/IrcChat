using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace IrcChat.Api.Services;

public class CloudinaryWrapper(Cloudinary cloudinary) : ICloudinaryWrapper
{
    public Url UrlImgUp => cloudinary.Api.UrlImgUp;
    public Task<DelResResult> DeleteResourcesAsync(DelResParams parameters, CancellationToken? cancellationToken = null)
        => cloudinary.DeleteResourcesAsync(parameters, cancellationToken);
    public Task<ListResourcesResult> ListResourcesAsync(ListResourcesParams parameters, CancellationToken? cancellationToken = null)
        => cloudinary.ListResourcesAsync(parameters, cancellationToken);
    public Task<ImageUploadResult> UploadAsync(ImageUploadParams uploadParams, CancellationToken? cancellationToken = null)
    => cloudinary.UploadAsync(uploadParams, cancellationToken);
    public Task<DeletionResult> DestroyAsync(DeletionParams parameters)
        => cloudinary.DestroyAsync(parameters);
}