using ElectronicStore.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElectronicStore.Controllers.Api;

/// <summary>
/// ADDR-02 — API nội bộ đổ dữ liệu cho selectbox địa chỉ.
/// </summary>
/// <remarks>
/// Dữ liệu đọc từ file JSON kèm repo nên không phụ thuộc dịch vụ bên ngoài. Đây là dữ liệu
/// công khai, không gắn với tài khoản nào, nên không cần đăng nhập; cũng vì vậy mà không có
/// tham số nào đụng tới dữ liệu người dùng.
/// </remarks>
[ApiController]
[Route("api/dia-gioi")]
[Produces("application/json")]
public class AdministrativeUnitsController : ControllerBase
{
    /// <summary>Danh sách không đổi lúc chạy nên cho trình duyệt cache 1 giờ.</summary>
    private const int CacheSeconds = 3600;

    private readonly IAdministrativeUnitService _units;

    public AdministrativeUnitsController(IAdministrativeUnitService units)
    {
        _units = units;
    }

    /// <summary>GET /api/dia-gioi/tinh-thanh — 34 tỉnh/thành phố.</summary>
    [HttpGet("tinh-thanh")]
    [ResponseCache(Duration = CacheSeconds, Location = ResponseCacheLocation.Any)]
    public ActionResult<IEnumerable<AdministrativeUnitDto>> GetProvinces()
    {
        if (!_units.IsAvailable)
        {
            return DatasetUnavailable();
        }

        return Ok(_units.GetProvinces()
            .Select(province => new AdministrativeUnitDto(province.Code, province.Name)));
    }

    /// <summary>
    /// GET /api/dia-gioi/phuong-xa?provinceCode=01 — phường/xã của một tỉnh.
    /// </summary>
    [HttpGet("phuong-xa")]
    [ResponseCache(Duration = CacheSeconds, Location = ResponseCacheLocation.Any)]
    public ActionResult<IEnumerable<AdministrativeUnitDto>> GetWards([FromQuery] string? provinceCode)
    {
        if (!_units.IsAvailable)
        {
            return DatasetUnavailable();
        }

        if (string.IsNullOrWhiteSpace(provinceCode))
        {
            return BadRequest(new { message = "Thiếu tham số provinceCode." });
        }

        if (_units.FindProvince(provinceCode) is null)
        {
            return NotFound(new { message = "Không tìm thấy tỉnh/thành phố với mã đã gửi." });
        }

        return Ok(_units.GetWards(provinceCode)
            .Select(ward => new AdministrativeUnitDto(ward.Code, ward.Name)));
    }

    /// <summary>
    /// 503 để client biết đây là sự cố phía server (thiếu/hỏng dataset) chứ không phải
    /// tỉnh đó không có phường/xã nào.
    /// </summary>
    private ObjectResult DatasetUnavailable() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { message = "Chưa nạp được dữ liệu địa giới hành chính." });
}

/// <summary>Một mục trong selectbox: mã để lưu, tên để hiển thị.</summary>
public sealed record AdministrativeUnitDto(string Code, string Name);
