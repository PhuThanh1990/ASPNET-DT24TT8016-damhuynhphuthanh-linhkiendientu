using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ElectronicStore.Areas.Admin.Services;

/// <summary>
/// Stores product images on disk under <c>wwwroot/uploads/products</c> and hands back the
/// relative URL that goes into <c>ProductImage.ImageUrl</c>. Image bytes never touch the
/// database — see docs/database-schema.md § 6.
/// </summary>
public sealed class ProductImageStorage
{
    /// <summary>Folder under wwwroot that holds every uploaded product image.</summary>
    public const string RelativeFolder = "uploads/products";

    public const long MaxFileSizeBytes = 2 * 1024 * 1024;

    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/pjpeg", "image/png", "image/webp"];

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProductImageStorage> _logger;

    public ProductImageStorage(IWebHostEnvironment environment, ILogger<ProductImageStorage> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Human readable list of what may be uploaded, for form hints.</summary>
    public static string AllowedDescription =>
        $"{string.Join(", ", AllowedExtensions)} — tối đa {MaxFileSizeBytes / (1024 * 1024)}MB";

    /// <summary>
    /// Checks a posted file against every rule and reports the first failure into
    /// <paramref name="modelState"/>.
    /// </summary>
    /// <returns><c>true</c> when the file is safe to store.</returns>
    public bool Validate(IFormFile? file, string modelStateKey, ModelStateDictionary modelState)
    {
        if (file is null || file.Length == 0)
        {
            modelState.AddModelError(modelStateKey, "Vui lòng chọn một file ảnh.");
            return false;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            modelState.AddModelError(modelStateKey,
                $"Ảnh tối đa {MaxFileSizeBytes / (1024 * 1024)}MB. File đang chọn nặng " +
                $"{file.Length / 1024d / 1024d:0.##}MB.");
            return false;
        }

        // Extension is taken from the client name for the check only; the stored name is
        // generated below, so nothing the client sends ever becomes a path.
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            modelState.AddModelError(modelStateKey,
                $"Chỉ chấp nhận file {string.Join(", ", AllowedExtensions)}.");
            return false;
        }

        if (!AllowedContentTypes.Contains(file.ContentType?.ToLowerInvariant()))
        {
            modelState.AddModelError(modelStateKey, "Kiểu nội dung của file không phải ảnh hợp lệ.");
            return false;
        }

        // Extension and content type are both attacker controlled, so the bytes decide:
        // an .exe renamed to .jpg with a faked content type is rejected here.
        if (!HasImageSignature(file, extension))
        {
            modelState.AddModelError(modelStateKey,
                "Nội dung file không khớp với định dạng ảnh. File có thể đã bị đổi đuôi.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Writes the file under wwwroot with a freshly generated name and returns the relative
    /// URL to store in the database. Call <see cref="Validate"/> first.
    /// </summary>
    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(WebRootPath, RelativeFolder);
        Directory.CreateDirectory(folder);

        // The client file name is discarded entirely: a GUID cannot traverse directories,
        // cannot collide with an existing file and cannot carry a second extension.
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(folder, fileName);

        // CreateNew rather than Create: if the impossible happens and the name is taken,
        // fail instead of overwriting somebody else's image.
        await using (var stream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/{RelativeFolder}/{fileName}";
    }

    /// <summary>
    /// Deletes a previously stored image. Missing files, empty paths and anything pointing
    /// outside the upload folder are ignored — removing the database row is what matters,
    /// an orphaned file must never take the request down with it.
    /// </summary>
    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return;
        }

        try
        {
            var uploadRoot = Path.GetFullPath(Path.Combine(WebRootPath, RelativeFolder));
            var candidate = Path.GetFullPath(
                Path.Combine(WebRootPath, relativeUrl.TrimStart('/', '\\')));

            // Defence in depth: the path comes from our own table, but a bad row must not
            // let a delete escape the upload folder.
            if (!candidate.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                _logger.LogWarning("Bỏ qua yêu cầu xóa ảnh nằm ngoài thư mục upload: {Path}", relativeUrl);
                return;
            }

            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The row is going away regardless; a locked or read-only file is a cleanup
            // problem, not a reason to fail the admin's action.
            _logger.LogWarning(ex, "Không xóa được file ảnh {Path}", relativeUrl);
        }
    }

    // In tests or odd hosting setups WebRootPath can be null; fall back to ContentRoot.
    private string WebRootPath =>
        _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

    /// <summary>Compares the leading bytes against the real signature of each allowed format.</summary>
    private static bool HasImageSignature(IFormFile file, string extension)
    {
        Span<byte> header = stackalloc byte[12];

        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        if (read < header.Length)
        {
            return false;
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[..8].SequenceEqual(PngSignature),
            // RIFF....WEBP
            ".webp" => header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
