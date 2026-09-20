using System.Text.Json;

namespace ElectronicStore.Helpers;

/// <summary>
/// ISession chỉ lưu được string và byte[], nên object phải đi qua JSON.
/// Chỉ dùng cho DTO thuần (như giỏ hàng) — không bao giờ nhét entity EF đang được
/// tracking vào session: entity kéo theo navigation property và trạng thái tracking,
/// serialize sẽ vòng lặp vô tận và dữ liệu trong session sẽ nhanh chóng lệch với database.
/// </summary>
public static class SessionExtensions
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void SetObject<T>(this ISession session, string key, T value) =>
        session.SetString(key, JsonSerializer.Serialize(value, Options));

    /// <summary>Trả về null khi chưa có dữ liệu hoặc dữ liệu cũ không còn đọc được.</summary>
    public static T? GetObject<T>(this ISession session, string key)
    {
        var json = session.GetString(key);

        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            // Session còn sót định dạng cũ sau khi đổi model: bỏ qua, coi như chưa có.
            session.Remove(key);
            return default;
        }
    }
}
