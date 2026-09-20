namespace ElectronicStore.Data;

/// <summary>
/// The reference catalog inserted on a fresh database so the team has something to work
/// with. Rows are matched by slug, so editing this file adds rows without duplicating the
/// existing ones. Real inventory is entered through the admin screens.
/// </summary>
public static class CatalogSeedData
{
    public record CategorySeed(string Name, string Slug, string Description, int SortOrder);

    public record BrandSeed(string Name, string Slug, string Description);

    public record ProductSeed(
        string Name,
        string Slug,
        string Sku,
        string CategorySlug,
        string BrandSlug,
        decimal Price,
        int StockQuantity,
        string ShortDescription,
        string Specifications);

    public static readonly CategorySeed[] Categories =
    [
        new("Vi điều khiển", "vi-dieu-khien", "Board vi điều khiển và kit phát triển.", 1),
        new("Cảm biến", "cam-bien", "Cảm biến nhiệt độ, độ ẩm, khoảng cách, ánh sáng...", 2),
        new("Module", "module", "Module mở rộng chức năng cho vi điều khiển.", 3),
        new("Nguồn", "nguon", "Nguồn tổ ong, mạch hạ áp, adapter.", 4),
        new("Relay", "relay", "Relay và module relay đóng ngắt tải.", 5)
    ];

    public static readonly BrandSeed[] Brands =
    [
        new("Espressif", "espressif", "Hãng sản xuất dòng chip ESP8266 và ESP32."),
        new("Arduino", "arduino", "Nền tảng phần cứng mã nguồn mở phổ biến nhất cho người mới."),
        new("Waveshare", "waveshare", "Module, màn hình và phụ kiện cho nhúng."),
        new("Generic", "generic", "Linh kiện phổ thông không gắn thương hiệu cụ thể.")
    ];

    public static readonly ProductSeed[] Products =
    [
        new(
            "ESP32 DevKit V1",
            "esp32-devkit-v1",
            "MCU-ESP32-DEVKIT-V1",
            "vi-dieu-khien",
            "espressif",
            145_000m,
            50,
            "Kit phát triển ESP32 38 chân, tích hợp WiFi và Bluetooth.",
            """{"Chip":"ESP32-WROOM-32","Nhân":"Dual-core 240MHz","Flash":"4MB","WiFi":"802.11 b/g/n","Bluetooth":"v4.2 BR/EDR + BLE","Số chân GPIO":"30","Điện áp hoạt động":"3.3V","Cổng nạp":"Micro USB"}"""),
        new(
            "Arduino Uno R3",
            "arduino-uno-r3",
            "MCU-ARD-UNO-R3",
            "vi-dieu-khien",
            "arduino",
            210_000m,
            40,
            "Board Arduino Uno R3 chip ATmega328P, phù hợp cho người mới bắt đầu.",
            """{"Vi điều khiển":"ATmega328P","Xung nhịp":"16MHz","Flash":"32KB","SRAM":"2KB","EEPROM":"1KB","Chân digital":"14 (6 chân PWM)","Chân analog":"6","Điện áp vào":"7-12V"}"""),
        new(
            "Cảm biến nhiệt độ độ ẩm DHT22",
            "cam-bien-dht22",
            "SEN-DHT22",
            "cam-bien",
            "generic",
            85_000m,
            100,
            "Cảm biến nhiệt độ và độ ẩm DHT22 (AM2302), độ chính xác cao hơn DHT11.",
            """{"Dải đo nhiệt độ":"-40°C đến 80°C","Sai số nhiệt độ":"±0.5°C","Dải đo độ ẩm":"0-100% RH","Sai số độ ẩm":"±2% RH","Điện áp hoạt động":"3.3-6V","Giao tiếp":"1-Wire","Chu kỳ đọc":"2 giây"}"""),
        new(
            "Cảm biến siêu âm HC-SR04",
            "cam-bien-hc-sr04",
            "SEN-HCSR04",
            "cam-bien",
            "generic",
            25_000m,
            200,
            "Cảm biến siêu âm đo khoảng cách HC-SR04, dùng nhiều trong robot tránh vật cản.",
            """{"Dải đo":"2cm - 400cm","Độ chính xác":"±3mm","Góc quét":"15°","Điện áp hoạt động":"5V","Dòng tiêu thụ":"15mA","Tần số":"40kHz","Chân tín hiệu":"Trig, Echo"}"""),
        new(
            "Module Relay 5V 1 kênh",
            "module-relay-5v-1-kenh",
            "MOD-RELAY-1CH-5V",
            "relay",
            "generic",
            18_000m,
            150,
            "Module relay 1 kênh 5V có opto cách ly, điều khiển tải AC/DC công suất nhỏ.",
            """{"Số kênh":"1","Điện áp điều khiển":"5V DC","Dòng kích":"15-20mA","Tải tối đa AC":"250V 10A","Tải tối đa DC":"30V 10A","Cách ly":"Opto PC817","Kiểu kích":"Mức thấp"}""")
    ];
}
