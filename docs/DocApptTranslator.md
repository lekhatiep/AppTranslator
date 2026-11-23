# Tài liệu nghiệp vụ – App dịch màn hình game (Phase 1)

Nov 21, 2026 · @tiep

## 1. Tổng quan

Phase 1 làm một app Windows chạy local, dịch phụ đề và lựa chọn hội thoại của The Witcher 3 từ tiếng Anh sang tiếng Việt, hiển thị đè đúng chỗ chữ gốc, độ trễ tối đa 1.5s.

- **Bối cảnh:** khách chơi PS5 từ xa qua Chiaki trên PC Windows 10, cửa sổ Borderless Windowed, điều khiển bằng tay cầm.
- **Mục tiêu dài hạn:** công cụ dịch màn hình cho nhiều game, mỗi game một hồ sơ (profile). Không phụ thuộc Chiaki.
- **Ưu tiên:** độ chính xác trước tốc độ. Không có deadline; nghiệm thu khi đạt tiêu chí ở mục 4.
- **Người dùng:** một người (khách), cài trên một PC.

## 2. Phạm vi

Phase 1 chỉ cam kết nghiệm thu cho phụ đề và lựa chọn hội thoại của Witcher 3; các loại chữ khác để Phase 2.

| Hạng mục | Phase 1 (nghiệm thu) | Phase 1 (best effort) | Phase 2 | Ngoài phạm vi |
| --- | --- | --- | --- | --- |
| Phụ đề Witcher 3 | ✓ |  |  |  |
| Lựa chọn hội thoại Witcher 3 | ✓ |  |  |  |
| Chế độ chọn vùng thủ công (vùng bất kỳ) |  | ✓ |  |  |
| Menu, mô tả vật phẩm, nhật ký nhiệm vụ, sách/thư (tự nhận diện) |  |  | ✓ |  |
| Game khác (qua hồ sơ game) |  | Kiến trúc sẵn sàng | ✓ |  |
| Điều khiển bằng tay cầm |  |  | ✓ |  |
| Dịch lời thoại (âm thanh) |  |  |  | ✓ |
| Steam Deck / SteamOS |  |  |  | ✓ |
| Exclusive Fullscreen |  |  |  | ✓ |
| Tích hợp vào mã nguồn Chiaki |  |  |  | ✓ |
| Chạy offline |  |  |  | ✓ |
| Danh sách câu thoại có sẵn (tra cứu trước cache và AI) |  | Thiết kế sẵn interface | Tùy chọn, khi khách có nguồn |  |

## 3. Yêu cầu chức năng

App gồm 13 chức năng; mã FR dùng để truy vết trong kế hoạch và nghiệm thu.

| Mã | Chức năng | Mô tả |
| --- | --- | --- |
| FR-01 | Chọn cửa sổ nguồn | Chọn cửa sổ bất kỳ (Chiaki hoặc game khác) để chụp; nhớ lựa chọn theo hồ sơ game |
| FR-02 | Chọn vùng dịch | Kéo chuột để khoanh vùng phụ đề và vùng lựa chọn; lưu theo tỉ lệ % cửa sổ để không lệch khi đổi kích thước |
| FR-03 | Bật/tắt dịch | Phím tắt toàn cục bật/tắt; phím tắt riêng để mở chế độ chọn vùng; có thể đổi phím trong cài đặt |
| FR-04 | Dịch phụ đề | Phát hiện chữ đổi, chờ chữ ổn định, OCR, ghép các dòng thành một câu, dịch, hiển thị; giữ dấu câu ("...", "?") |
| FR-05 | Dịch lựa chọn hội thoại | Tách từng lựa chọn, bỏ số thứ tự và ▶ khi dịch; hiển thị lại số thứ tự, ▶ và màu nổi bật cho dòng đang chọn; không dịch lại khi chỉ di chuyển lựa chọn |
| FR-06 | Overlay | Cửa sổ trong suốt, luôn nằm trên, click-through, không giành focus; bản dịch đè đúng vị trí chữ gốc, tự co chữ/xuống dòng và mở rộng lên trên khi bản dịch dài; bám theo cửa sổ nguồn khi di chuyển |
| FR-07 | Chọn vùng thủ công (best effort) | Khoanh vùng bất kỳ để dịch một lần khối chữ bên trong; độ trễ được nới |
| FR-08 | Hồ sơ game | Tạo/sửa/chọn hồ sơ: tên game, vùng chữ, màu chữ, văn phong, xưng hô, tên riêng giữ nguyên, bảng thuật ngữ |
| FR-09 | Ngữ cảnh dịch | Gửi kèm hồ sơ game và vài câu trước đó cho AI để giữ xưng hô và văn phong nhất quán |
| FR-10 | Cache bản dịch | Câu đã dịch hiện ngay, không gọi API lại; có nút xóa cache theo hồ sơ |
| FR-11 | Cài đặt | Nhập API key (lưu mã hóa), chọn nhà cung cấp dịch, cỡ chữ, màu nền và độ mờ overlay, phím tắt |
| FR-12 | Lịch sử và log | Xem lại các câu đã dịch gần đây; log lỗi và độ trễ từng bước để hỗ trợ tinh chỉnh |
| FR-13 | Báo lỗi cho người dùng | Hiển thị trạng thái ngắn trên overlay khi mất mạng, hết quota, key sai; không làm treo app |

## 4. Yêu cầu phi chức năng và tiêu chí nghiệm thu

Nghiệm thu khi phụ đề đạt 95% OCR và 95% dịch đúng nghĩa, lựa chọn hội thoại đạt 90%, trên bộ mẫu khách cung cấp.

### Tiêu chí nghiệm thu

| Mã | Tiêu chí | Ngưỡng | Cách đo |
| --- | --- | --- | --- |
| AC-01 | OCR phụ đề | ≥ 95% | Tỉ lệ từ đúng so với bản chép chuẩn, trên bộ 50–100 câu |
| AC-02 | Dịch phụ đề không sai nghĩa | ≥ 95% | Khách chấm từng câu: đạt / sai nghĩa |
| AC-03 | OCR lựa chọn hội thoại | ≥ 90% | Tỉ lệ từ đúng, bộ mẫu riêng |
| AC-04 | Dịch lựa chọn không sai nghĩa | ≥ 90% | Khách chấm |
| AC-05 | Độ trễ phụ đề | ≤ 1.5s (p90) | Từ lúc chữ ổn định đến lúc hiển thị, đo bằng log; khi xung đột với độ chính xác thì ưu tiên chính xác |
| AC-06 | Overlay | Đạt/không | Hiện đúng vị trí trên Chiaki Borderless; không giành focus; tay cầm vẫn điều khiển game bình thường |

Bộ mẫu phải có cả cảnh sáng và tối, ảnh gốc 1080p chụp từ cửa sổ Chiaki. Khách cung cấp bản chép chuẩn và chấm bản dịch. Cách đo p90 cho độ trễ là đề xuất, cần khách xác nhận.

### Phi chức năng

- **Môi trường:** Windows 10 Pro 22H2 (build 19045) trở lên; i7-12700F, RAM 16GB, RTX 4060.
- **Hiệu năng:** không làm giật stream Chiaki; mục tiêu CPU trung bình dưới 10%, RAM dưới 500MB (đề xuất, đo lại ở PoC).
- **Mạng:** cần internet để dịch; khách đã chấp nhận điều khoản dữ liệu của Gemini bản miễn phí.
- **Bảo mật:** API key lưu mã hóa bằng Windows DPAPI, không ghi ra log.
- **Đóng gói:** bản self-contained, giải nén là chạy, không cần cài .NET.
- **Bảo trì:** OCR, dịch, chụp màn hình đặt sau interface để thay engine không sửa luồng chính; cấu hình theo game nằm trong hồ sơ, không viết cứng.

## 5. Công nghệ

Chọn C# .NET 8 + WPF vì khách biết .NET, dễ bảo trì, đóng gói self-contained chạy local; engine OCR chốt sau benchmark.

| Thành phần | Lựa chọn | Trạng thái | Ghi chú |
| --- | --- | --- | --- |
| Ngôn ngữ, runtime | C# / .NET 8 (LTS) | Chốt | Có thể lên .NET 10 LTS nếu đã phát hành ổn định |
| UI, overlay | WPF + Win32 interop (WS\_EX\_TRANSPARENT, WS\_EX\_NOACTIVATE, TOPMOST) | Chốt | Overlay click-through, không giành focus |
| Chụp màn hình | Windows.Graphics.Capture | Chốt | Chụp đúng cửa sổ, kể cả khi bị che |
| Xử lý ảnh | OpenCvSharp4 | Đề xuất | Tách chữ trắng/vàng, so khung hình, phóng to trước OCR |
| OCR | Ứng viên: Windows.Media.Ocr; PaddleOCR qua ONNX Runtime (DirectML trên RTX 4060) | Chốt sau benchmark | Chọn bằng số liệu ở Stage 3 |
| Dịch | Gemini API (demo bằng key miễn phí) | Chốt cho demo | Sau interface `ITranslator`; dự phòng Google Cloud Translation; model cụ thể chọn theo độ trễ đo được |
| Phím tắt | Win32 RegisterHotKey | Chốt |  |
| Lưu trữ | JSON (cấu hình, hồ sơ) + SQLite (cache, lịch sử) | Đề xuất | Chi tiết ở mục 7 |
| Kiến trúc app | MVVM (CommunityToolkit.Mvvm) + Microsoft.Extensions.Hosting (DI, config) | Đề xuất |  |
| Log | Serilog, ghi file theo ngày | Đề xuất |  |
| Kiểm thử | xUnit + tool benchmark chạy trên thư mục ảnh mẫu | Đề xuất |  |
| Đóng gói | `dotnet publish` self-contained, win-x64, file zip | Chốt | Installer để sau |

Python dựng thử nhanh hơn nhưng đóng gói nặng và khác nền tảng với khách; Electron quá nặng cho một overlay. Cả hai không chọn.

## 6. Kiến trúc và cấu trúc dự án

App là một pipeline 8 bước; mỗi bước có thể thay engine nhờ interface, và mọi cấu hình theo game đọc từ hồ sơ.

&#91;embedded content: luồng xử lý phụ đề · 8 bước\]

Pipeline chỉ chạy OCR khi vùng chữ thay đổi và đã đứng yên; câu lặp lại bỏ qua bước gọi Gemini. Lựa chọn hội thoại đi cùng luồng, thay bước ghép câu bằng bước tách từng lựa chọn.

### Cấu trúc solution

```
AppTranslator/
  src/
    AppTranslator.App/                 WPF: overlay, cài đặt, chọn vùng, ViewModel, DI, phím tắt
    AppTranslator.Core/                model + interface + pipeline, không phụ thuộc Windows
      Abstractions/                     IScreenCapture, IOcrEngine, ITranslator,
                                        ITranslationCache, IProfileStore
      Pipeline/                         ChangeDetector, StabilityGate, SentenceAssembler,
                                        ChoiceParser, TranslationPipeline
      Profiles/                         GameProfile, Region, StyleOptions
    AppTranslator.Capture.Windows/     Windows.Graphics.Capture, chọn cửa sổ
    AppTranslator.Imaging/             OpenCvSharp: tách màu chữ, so khung, phóng to
    AppTranslator.Ocr/                 WindowsOcrEngine, PaddleOcrEngine (ONNX)
    AppTranslator.Translation/         GeminiTranslator, GoogleTranslator, PromptBuilder
    AppTranslator.Storage/             hồ sơ JSON, SQLite (cache, lịch sử, số đo), DPAPI
  tools/
    AppTranslator.Benchmark/           console: chạy OCR/dịch trên thư mục ảnh, xuất CSV
  tests/
    AppTranslator.Core.Tests/
    AppTranslator.Ocr.Tests/
  samples/witcher3/                     images/, ground-truth.csv (bản chép chuẩn)
  profiles/                             default.json, witcher3.json (đóng gói kèm app)
```

- **Core** không tham chiếu API Windows, nên pipeline test được bằng ảnh tĩnh.
- **Benchmark** dùng chung Core và Ocr, là công cụ cho Gate 1 và đo nghiệm thu Stage 9.
- Thêm game mới = thêm một file hồ sơ, không sửa code; thêm engine mới = một class triển khai interface.

## 7. Lưu trữ dữ liệu

Không cần database server: cấu hình và hồ sơ game lưu file JSON (người dùng sửa, chia sẻ được), cache và lịch sử lưu SQLite (tra cứu nhanh, dữ liệu tăng dần). Tất cả nằm trong `%AppData%\AppTranslator\`.

```
%AppData%\AppTranslator\
  settings.json          cài đặt chung, phím tắt, nhà cung cấp dịch
  secrets.bin            API key, mã hóa DPAPI
  profiles\
    default.json         hồ sơ mặc định, giọng trung tính
    witcher3.json        hồ sơ The Witcher 3
  data\translator.db     SQLite: cache, lịch sử, số đo
  logs\app-YYYYMMDD.log
```

### Hồ sơ game (JSON)

```json
{
  "id": "witcher3",
  "gameName": "The Witcher 3: Wild Hunt",
  "windowTitleHint": "Chiaki",
  "regions": [
    { "type": "subtitle", "x": 0.0, "y": 0.80, "w": 1.0, "h": 0.12,
      "textColor": "white", "maxLines": 3 },
    { "type": "choices", "x": 0.55, "y": 0.40, "w": 0.40, "h": 0.30,
      "textColor": "yellow", "selectedMarker": "▶" }
  ],
  "style": {
    "tone": "Trung cổ kỳ ảo, lời thoại tự nhiên",
    "addressing": "Geralt xưng 'ta', gọi người lạ 'ngươi'",
    "contextLines": 3
  },
  "keepNames": ["Geralt", "Yennefer", "Triss", "Kaer Morhen"],
  "glossary": [ { "en": "the Wild Hunt", "vi": "Đoàn Săn Hoang Dã" } ],
  "version": 1
}
```

Tọa độ vùng lưu theo tỉ lệ 0–1 của cửa sổ. Giá trị trong ví dụ (vùng, văn phong, thuật ngữ) chỉ minh họa; khách sẽ cung cấp và duyệt giá trị thật.

### SQLite: translator.db

| Bảng | Cột chính | Dùng để |
| --- | --- | --- |
| `TranslationCache` | `Id`, `ProfileId`, `SourceHash` (SHA-256 của câu đã chuẩn hóa), `SourceText`, `TranslatedText`, `Provider`, `Model`, `ProfileVersion`, `CreatedAt`, `HitCount` | Câu lặp hiện ngay; khóa duy nhất (`ProfileId`, `SourceHash`, `ProfileVersion`) để đổi hồ sơ thì cache cũ không bị dùng lại |
| `TranslationHistory` | `Id`, `ProfileId`, `RegionType`, `SourceText`, `TranslatedText`, `FromCache`, `CreatedAt` | Xem lại câu gần đây (FR-12); giữ N ngày gần nhất |
| `PipelineMetric` | `Id`, `HistoryId`, `CaptureMs`, `OcrMs`, `TranslateMs`, `RenderMs`, `TotalMs`, `OcrEngine`, `CreatedAt` | Đo độ trễ cho AC-05 và tinh chỉnh |

Truy cập SQLite qua Microsoft.Data.Sqlite + Dapper (nhẹ, ít phụ thuộc); có thể thay bằng EF Core nếu đội quen hơn. Không lưu ảnh chụp màn hình, trừ khi bật chế độ debug.

## 8. Kế hoạch theo stage

Phase 1 chia 10 stage nhỏ, mỗi stage kết thúc bằng một đầu ra chạy được; Stage 1–3 và 5 làm ngay được, không cần chờ thêm ảnh. Quy mô S/M/L là ước lượng tương đối, chưa phải cam kết thời gian.

| Stage | Nội dung | FR liên quan | Cần từ khách | Quy mô | Đầu ra / điều kiện xong |
| --- | --- | --- | --- | --- | --- |
| 0. Chuẩn bị | Repo, solution, các interface, DI, log, settings JSON, hồ sơ mặc định | FR-08, FR-11 | Không | S | Solution build được, chạy app rỗng |
| 1. Chụp và chọn vùng | Chọn cửa sổ, chụp bằng Windows.Graphics.Capture, kéo chuột chọn vùng, lưu vào hồ sơ | FR-01, FR-02 | Không | M | Chụp được vùng của cửa sổ bất kỳ, kể cả Chiaki |
| 2. Overlay và phím tắt | Cửa sổ trong suốt, click-through, không focus, bám cửa sổ nguồn; phím tắt toàn cục | FR-03, FR-06 | Không | M | Vẽ chữ giả đè lên Chiaki mà tay cầm vẫn điều khiển game |
| 3. Benchmark OCR | Tool chạy các engine trên thư mục ảnh, so bản chép chuẩn, xuất bảng kết quả | — | Ảnh mẫu (đã có 3, cần 10–15) | M | **Gate 1:** chọn engine OCR và cách xử lý cảnh sáng |
| 4. Nhận diện phụ đề | So khung hình, chờ chữ ổn định, tách chữ trắng, OCR nhiều khung lấy đa số, ghép dòng thành câu | FR-04 | Ảnh mẫu | L | Ra đúng câu tiếng Anh từ ảnh/cửa sổ thật |
| 5. Dịch bằng Gemini | `ITranslator`, prompt từ hồ sơ, ngữ cảnh câu trước, cache SQLite, xử lý lỗi và quota | FR-09, FR-10, FR-13 | Key Gemini | M | Dịch được câu mẫu, đo được độ trễ |
| 6. Ghép pipeline phụ đề | Nối Stage 1–5, bố cục tiếng Việt (co chữ, xuống dòng lên trên), đo độ trễ từng bước | FR-04, FR-06, FR-12 | — | M | **Gate 2:** demo phụ đề cho khách; khách quyết ngân sách |
| 7. Lựa chọn hội thoại | Vùng thứ hai, tách từng lựa chọn, nhận dòng đang chọn, giữ ▶ và màu | FR-05 | Ảnh lựa chọn hội thoại | M | Dịch được lựa chọn, không dịch lại khi di chuyển |
| 8. Chọn vùng thủ công | Khoanh vùng bất kỳ, dịch một lần (best effort) | FR-07 | Ít | M | Chạy được với vật phẩm, nhật ký ở mức tham khảo |
| 9. Hoàn thiện và nghiệm thu | UI cài đặt, hồ sơ Witcher 3 thật, đo trên bộ mẫu, tinh chỉnh, đóng gói, hướng dẫn cài | FR-08, FR-11 | Bộ mẫu 50–100 câu + bản chép chuẩn; thuật ngữ, văn phong | M | **Gate 3:** đạt AC-01 đến AC-06 |

Thứ tự có thể song song: Stage 2 và 5 không phụ thuộc nhau; Stage 4 chờ Gate 1.

## 9. Giả định, rủi ro và điểm còn mở

Rủi ro lớn nhất là OCR ở cảnh sáng, vì phụ đề Witcher 3 không có nền; Gate 1 sẽ xác nhận mốc 95% có khả thi không.

### Giả định

- Khách stream Chiaki 1080p với bitrate ổn định; cấu hình thấp hơn không nằm trong cam kết nghiệm thu.
- Phụ đề tiếng Anh bật ở cỡ lớn nhất; game và Chiaki chạy Windowed/Borderless.
- Vị trí phụ đề cố định (khoảng 82–85% chiều cao, căn giữa) theo 3 ảnh mẫu.
- PC luôn có internet; Gemini bản miễn phí đủ quota cho demo.
- Khách cung cấp bộ mẫu, bản chép chuẩn, chấm bản dịch, duyệt văn phong và thuật ngữ.

### Rủi ro

| Rủi ro | Ảnh hưởng | Cách giảm |
| --- | --- | --- |
| Chữ trắng mảnh, không nền, khó đọc ở cảnh sáng | Không đạt AC-01 | Tách màu + viền, OCR nhiều khung lấy đa số, so 2–3 engine |
| Stream nén làm mờ chữ | Giảm độ chính xác OCR | Benchmark trên ảnh chụp từ Chiaki thật; khuyến nghị cấu hình stream |
| Gemini chậm hoặc dao động | Vượt 1.5s | Model nhỏ hơn, cache, dự phòng Google Translate |
| Quota miễn phí hết giữa phiên chơi | Mất dịch | Báo trạng thái trên overlay; cache giảm số lượt gọi |
| Xưng hô sai do phụ đề không có tên người nói | Giảm điểm AC-02 | Hồ sơ game + ngữ cảnh câu trước; thống nhất cách chấm xưng hô với khách |
| Bản dịch dài hơn che mất cảnh | Trải nghiệm kém | Co chữ, xuống dòng, cho chỉnh độ mờ nền |
| Windows 10 đã hết hỗ trợ | API Windows có thể thay đổi về sau | Chỉ dùng API có sẵn từ 1903; test thêm trên Windows 11 khi có thể |

### Điểm còn mở

- [ ] 10–15 ảnh gốc từ cửa sổ Chiaki (cảnh rất sáng, tối, lựa chọn hội thoại) kèm cấu hình stream; xác nhận ảnh 1920×1080 đã gửi chụp từ Chiaki hay từ PS5
- [ ] Key Gemini để demo
- [ ] Ngân sách dịch vụ trả phí sau Gate 2
- [ ] Bộ mẫu nghiệm thu 50–100 câu kèm bản chép chuẩn
- [ ] Văn phong, xưng hô, danh sách tên riêng, bảng thuật ngữ Witcher 3
- [ ] Xác nhận cách đo độ trễ theo p90 (AC-05)
