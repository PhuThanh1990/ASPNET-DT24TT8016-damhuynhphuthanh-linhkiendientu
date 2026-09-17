# Dữ liệu địa giới hành chính Việt Nam

Dùng cho selectbox Tỉnh/Thành phố và Phường/Xã ở form địa chỉ giao hàng (ADDR-02).

| | |
| --- | --- |
| **Nguồn** | [ThangLeQuoc/vietnamese-provinces-database](https://github.com/ThangLeQuoc/vietnamese-provinces-database) |
| **File gốc** | `json/vn_only_simplified_json_generated_data_vn_units.json` |
| **Phiên bản** | v5.1.0 — sinh ngày 2026-09-12, theo Nghị quyết 36/2026/QH16 |
| **Giấy phép** | MIT — xem `LICENSE-vietnamese-provinces-database.txt` |
| **Quy mô** | 34 tỉnh/thành phố · 3.321 phường/xã |

File `vietnam-administrative-units.json` là **bản sao nguyên trạng** của file gốc, không
chỉnh sửa. `vietnam-administrative-units.metadata.json` là file metadata đi kèm của
upstream, dùng để log phiên bản dataset lúc khởi động.

## Vì sao chọn dataset này

- Dùng **mã chuẩn của Tổng cục Thống kê** (`01` = Thành phố Hà Nội,
  `00004` = Phường Ba Đình), không phải mã tự đặt. Đây là mã mà các API vận chuyển
  (GHN, GHTK, VNPost) đối chiếu, nên sau này tích hợp sẽ không phải map lại.
- Đã cập nhật theo cơ cấu hành chính 2 cấp (bỏ cấp quận/huyện) áp dụng từ 01/07/2025.
- Giấy phép MIT, cho phép kèm thẳng vào repo.

## Cấu trúc

```jsonc
[
  {
    "Code": "01",                      // mã tỉnh, 2 chữ số
    "FullName": "Thành phố Hà Nội",
    "PostalCodePrefix": "10, 11, 12, 13, 14",
    "Wards": [
      {
        "Code": "00004",               // mã phường/xã, 5 chữ số
        "FullName": "Phường Ba Đình",
        "ProvinceCode": "01",
        "PostalCode": "11120"
      }
    ]
  }
]
```

## Cập nhật khi có nghị quyết mới

```bash
BASE=https://raw.githubusercontent.com/ThangLeQuoc/vietnamese-provinces-database/master/json
curl -o Data/AdministrativeUnits/vietnam-administrative-units.json \
  "$BASE/vn_only_simplified_json_generated_data_vn_units.json"
curl -o Data/AdministrativeUnits/vietnam-administrative-units.metadata.json \
  "$BASE/vn_provinces_metadata.json"
```

Địa chỉ đã lưu không bị ảnh hưởng: `Address` lưu **cả mã lẫn tên** tại thời điểm đặt,
nên tên cũ vẫn hiển thị đúng kể cả khi dataset đổi tên đơn vị hành chính.
