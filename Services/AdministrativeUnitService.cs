using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElectronicStore.Services;

/// <inheritdoc cref="IAdministrativeUnitService"/>
public sealed class AdministrativeUnitService : IAdministrativeUnitService
{
    private const string DataFolder = "Data/AdministrativeUnits";
    private const string DataFileName = "vietnam-administrative-units.json";
    private const string MetadataFileName = "vietnam-administrative-units.metadata.json";

    /// <summary>Tên đầy đủ có tiền tố đơn vị; bỏ tiền tố ra để sắp A→Z cho dễ tra.</summary>
    private static readonly string[] SortPrefixes =
        ["Thành phố ", "Tỉnh ", "Phường ", "Xã ", "Thị trấn ", "Đặc khu "];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IReadOnlyList<ProvinceOption> _provinces;
    private readonly IReadOnlyDictionary<string, ProvinceOption> _provincesByCode;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<WardOption>> _wardsByProvince;
    private readonly IReadOnlyDictionary<string, WardOption> _wardsByCode;

    public AdministrativeUnitService(IWebHostEnvironment environment, ILogger<AdministrativeUnitService> logger)
    {
        var path = Path.Combine(environment.ContentRootPath, DataFolder, DataFileName);

        try
        {
            using var stream = File.OpenRead(path);
            var raw = JsonSerializer.Deserialize<List<ProvinceRecord>>(stream, JsonOptions)
                ?? throw new InvalidDataException("Nội dung dataset rỗng.");

            var comparer = CreateVietnameseComparer();

            _provinces = raw
                .Where(province => !string.IsNullOrWhiteSpace(province.Code)
                    && !string.IsNullOrWhiteSpace(province.FullName))
                .Select(province => new ProvinceOption(province.Code!, province.FullName!))
                .OrderBy(province => StripPrefix(province.Name), comparer)
                .ToList();

            _provincesByCode = _provinces.ToDictionary(province => province.Code, StringComparer.Ordinal);

            _wardsByProvince = raw
                .Where(province => !string.IsNullOrWhiteSpace(province.Code))
                .ToDictionary(
                    province => province.Code!,
                    province => (IReadOnlyList<WardOption>)(province.Wards ?? [])
                        .Where(ward => !string.IsNullOrWhiteSpace(ward.Code)
                            && !string.IsNullOrWhiteSpace(ward.FullName))
                        .Select(ward => new WardOption(ward.Code!, ward.FullName!, province.Code!))
                        .OrderBy(ward => StripPrefix(ward.Name), comparer)
                        .ToList(),
                    StringComparer.Ordinal);

            // Mã phường/xã là duy nhất trên toàn quốc, nên một từ điển phẳng là đủ để tra
            // ngược ra tỉnh khi cần kiểm tra chéo.
            _wardsByCode = _wardsByProvince.Values
                .SelectMany(wards => wards)
                .GroupBy(ward => ward.Code, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            DatasetVersion = ReadDatasetVersion(environment, logger);
            IsAvailable = _provinces.Count > 0;

            logger.LogInformation(
                "Đã nạp dữ liệu hành chính {Version}: {ProvinceCount} tỉnh/thành, {WardCount} phường/xã.",
                DatasetVersion ?? "(không rõ phiên bản)", _provinces.Count, _wardsByCode.Count);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException
            or UnauthorizedAccessException or NotSupportedException)
        {
            // Thiếu file hay file hỏng không được làm sập cả website: form địa chỉ sẽ báo
            // lỗi riêng, các trang khác vẫn chạy bình thường.
            logger.LogError(ex,
                "Không đọc được dữ liệu hành chính tại {Path}. Form địa chỉ sẽ không có danh sách để chọn.",
                path);

            _provinces = [];
            _provincesByCode = new Dictionary<string, ProvinceOption>();
            _wardsByProvince = new Dictionary<string, IReadOnlyList<WardOption>>();
            _wardsByCode = new Dictionary<string, WardOption>();
            IsAvailable = false;
        }
    }

    public bool IsAvailable { get; }

    public string? DatasetVersion { get; }

    public IReadOnlyList<ProvinceOption> GetProvinces() => _provinces;

    public IReadOnlyList<WardOption> GetWards(string? provinceCode) =>
        provinceCode is not null && _wardsByProvince.TryGetValue(provinceCode, out var wards)
            ? wards
            : [];

    public ProvinceOption? FindProvince(string? provinceCode) =>
        provinceCode is not null && _provincesByCode.TryGetValue(provinceCode, out var province)
            ? province
            : null;

    public WardOption? FindWard(string? provinceCode, string? wardCode)
    {
        if (provinceCode is null || wardCode is null)
        {
            return null;
        }

        // Kiểm tra cả quan hệ cha-con: client có thể gửi lên một mã phường có thật nhưng
        // thuộc tỉnh khác, ghép lại sẽ ra địa chỉ không tồn tại.
        return _wardsByCode.TryGetValue(wardCode, out var ward)
            && string.Equals(ward.ProvinceCode, provinceCode, StringComparison.Ordinal)
                ? ward
                : null;
    }

    /// <summary>Đọc phiên bản dataset để log; thiếu file này cũng không sao.</summary>
    private static string? ReadDatasetVersion(IWebHostEnvironment environment, ILogger logger)
    {
        var path = Path.Combine(environment.ContentRootPath, DataFolder, MetadataFileName);

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<DatasetMetadata>(stream, JsonOptions)?.DatasetVersion;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogDebug(ex, "Không đọc được metadata dataset tại {Path}.", path);
            return null;
        }
    }

    private static StringComparer CreateVietnameseComparer()
    {
        try
        {
            return StringComparer.Create(CultureInfo.GetCultureInfo("vi-VN"), ignoreCase: true);
        }
        catch (CultureNotFoundException)
        {
            // Môi trường chạy với invariant globalization thì không có culture vi-VN.
            return StringComparer.InvariantCultureIgnoreCase;
        }
    }

    private static string StripPrefix(string name)
    {
        foreach (var prefix in SortPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name[prefix.Length..];
            }
        }

        return name;
    }

    // Khớp đúng cấu trúc file gốc của vietnamese-provinces-database (xem README cạnh file).
    private sealed record ProvinceRecord(
        [property: JsonPropertyName("Code")] string? Code,
        [property: JsonPropertyName("FullName")] string? FullName,
        [property: JsonPropertyName("Wards")] List<WardRecord>? Wards);

    private sealed record WardRecord(
        [property: JsonPropertyName("Code")] string? Code,
        [property: JsonPropertyName("FullName")] string? FullName);

    private sealed record DatasetMetadata(string? DatasetVersion);
}
