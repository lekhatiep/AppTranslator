# Implement Plan – AppTranslator Phase 1

Nguồn yêu cầu: [docs/DocApptTranslator.docx](docs/DocApptTranslator.docx) (FR-01..FR-13, AC-01..AC-06).
Đánh dấu: `[ ]` chưa làm · `[~]` đang làm · `[x]` xong · `[!]` bị chặn (ghi lý do).

## Quyết định đã chốt

| Ngày | Quyết định | Ghi chú |
|---|---|---|
| 2026-11-22 | Runtime: **.NET 8** (`net8.0-windows10.0.19041.0`) | Build bằng SDK 9 có sẵn trên máy. .NET 8 hết hỗ trợ 10/11/2026, nâng lên .NET 10 khi cần |
| 2026-11-22 | Chụp màn hình: làm **cả hai** engine sau `IScreenCapture` | (a) Windows.Graphics.Capture: chụp đúng cửa sổ, nhưng trên Win10 có viền vàng. (b) DXGI Desktop Duplication: không viền, overlay loại khỏi ảnh bằng `WDA_EXCLUDEFROMCAPTURE`, không chụp được khi cửa sổ bị che. Chọn engine trong cài đặt |
| 2026-11-22 | Chưa có key Gemini | Stage 5 làm trước bằng `MockTranslator`, khi có key thì nối Gemini thật |

## Đang chờ từ khách / người dùng

- [ ] Ảnh mẫu Witcher 3 (3 ảnh đã có, cần 10–15) → `samples/witcher3/images/` *(chặn Stage 3, 4)*
- [ ] Bản chép chuẩn `samples/witcher3/ground-truth.csv` *(chặn Stage 3)*
- [ ] Key Gemini *(chặn phần Gemini thật của Stage 5)*
- [ ] Ảnh lựa chọn hội thoại *(chặn Stage 7)*
- [ ] Văn phong, xưng hô, tên riêng, bảng thuật ngữ Witcher 3 *(Stage 9)*
- [ ] Bộ mẫu nghiệm thu 50–100 câu + bản chép chuẩn *(Stage 9)*
- [ ] Xác nhận đo độ trễ theo p90 (AC-05)
- [ ] Ngân sách dịch vụ trả phí (sau Gate 2)

## Thứ tự

`0 → 1 → (2 ∥ 5) → 3 → 4 → 6 → 7 → 8 → 9`. Stage 3 bắt đầu khi có ảnh; Stage 4 chờ Gate 1.

---

## Stage 0 – Chuẩn bị (S) · FR-08, FR-11 · ✅ xong 2026-11-23
- [x] `git init`, `.gitignore`, `Directory.Build.props` (biến `$(WindowsTfm)` = net8.0-windows10.0.19041.0, nullable, LangVersion)
- [x] Solution + project: App, Core, Capture.Windows, Imaging, Ocr, Translation, Storage, tests/Core.Tests
  - tools/Benchmark và tests/Ocr.Tests dời sang Stage 3 (chưa có gì để chứa)
  - TFM: Core, Imaging, Translation, Storage = `net8.0`; App, Capture.Windows, Ocr = `$(WindowsTfm)`
- [x] Core/Abstractions: `IScreenCapture`, `IOcrEngine`, `ITranslator`, `ITranslationCache`, `IProfileStore`
  - Ảnh qua Core dùng `Frame` (BGRA byte[]), không phụ thuộc OpenCV; Imaging tự đổi sang `Mat`
- [x] Core/Profiles: `GameProfile`, `Region` (`RegionType`), `StyleOptions`, `GlossaryEntry`; Core/Settings: `AppSettings`
- [x] App: Generic Host + DI, Serilog (file theo ngày vào `%AppData%\AppTranslator\logs`), CommunityToolkit.Mvvm; cửa sổ chính chọn hồ sơ
- [x] Storage: `AppPaths`, `JsonSettingsStore` (tạo mặc định lần đầu), `JsonProfileStore` (`SeedMissing` chép hồ sơ đóng gói, không ghi đè)
- [x] `profiles/default.json`, `profiles/witcher3.json` (nháp, theo ví dụ tài liệu)
- **Đã kiểm:** build 0 warning; 4/4 test Storage đạt; app chạy, tạo `settings.json`, chép 2 hồ sơ, ghi log

## Stage 1 – Chụp và chọn vùng (M) · FR-01, FR-02
- [ ] Liệt kê cửa sổ (EnumWindows), chọn cửa sổ, nhớ theo `windowTitleHint` của hồ sơ
- [ ] `WgcCapture` (Windows.Graphics.Capture) → `Mat`
- [ ] `DxgiCapture` (Desktop Duplication, cắt theo vùng cửa sổ) → `Mat`
- [ ] Chọn engine chụp trong `settings.json`
- [ ] UI chọn vùng: kéo chuột trên ảnh chụp, lưu tọa độ 0–1 vào hồ sơ
- [ ] Chế độ debug: lưu PNG vùng chụp
- **Xong khi:** chụp đúng vùng phụ đề của Chiaki bằng cả hai engine

## Stage 2 – Overlay và phím tắt (M) · FR-03, FR-06
- [ ] Overlay WPF trong suốt: `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, TOPMOST
- [ ] `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` cho overlay
- [ ] Bám cửa sổ nguồn (`SetWinEventHook` LOCATIONCHANGE), xử lý DPI
- [ ] Phím tắt toàn cục (`RegisterHotKey`): bật/tắt dịch, mở chọn vùng; đổi được trong cài đặt
- **Xong khi:** chữ giả đè lên Chiaki, tay cầm vẫn điều khiển game, overlay không giành focus

## Stage 3 – Benchmark OCR (M) · Gate 1
- [ ] Tạo project tools/AppTranslator.Benchmark và tests/AppTranslator.Ocr.Tests
- [ ] Tool console: đọc ảnh + `ground-truth.csv`, chạy engine × tiền xử lý, xuất CSV (độ chính xác theo từ, ms)
- [ ] `WindowsOcrEngine` (kiểm tra gói ngôn ngữ English OCR, báo nếu thiếu)
- [ ] `PaddleOcrEngine` (ONNX Runtime, DirectML)
- [ ] (Tùy chọn) Tesseract làm mốc so sánh
- [ ] Tiền xử lý: tách màu trắng/vàng (HSV), phóng to 2–3×, nhị phân hóa, lọc viền
- [ ] **Gate 1:** chốt engine + tiền xử lý, ghi kết quả vào mục “Quyết định đã chốt”

## Stage 4 – Nhận diện phụ đề (L) · FR-04
- [ ] `ChangeDetector` (so mặt nạ chữ giữa các khung)
- [ ] `StabilityGate` (chữ đứng yên N ms)
- [ ] OCR 2–3 khung, lấy đa số
- [ ] `SentenceAssembler` (ghép dòng, giữ dấu câu)
- [ ] Unit test trên ảnh tĩnh
- **Xong khi:** ra đúng câu tiếng Anh từ ảnh mẫu và cửa sổ thật

## Stage 5 – Dịch (M) · FR-09, FR-10, FR-13
- [ ] `MockTranslator` (dùng khi chưa có key)
- [ ] `PromptBuilder`: văn phong, xưng hô, `keepNames`, glossary, N câu trước
- [ ] `GeminiTranslator` (REST, HttpClient) *(chờ key)*
- [ ] Lưu key bằng DPAPI (`secrets.bin`), không ghi ra log
- [ ] SQLite (Microsoft.Data.Sqlite + Dapper): `TranslationCache`, khóa (ProfileId, SourceHash, ProfileVersion); xóa cache theo hồ sơ
- [ ] Xử lý lỗi 401 / 429 / timeout / mất mạng → trạng thái trên overlay
- [ ] Bỏ qua câu trùng câu trước và câu có độ tin cậy OCR thấp
- **Xong khi:** dịch được câu mẫu, đo được độ trễ

## Stage 6 – Ghép pipeline phụ đề (M) · FR-04, FR-06, FR-12 · Gate 2
- [ ] `TranslationPipeline` dùng `Channel<T>`
- [ ] Bố cục tiếng Việt: co chữ, xuống dòng, mở rộng lên trên
- [ ] `PipelineMetric` + `TranslationHistory`; màn xem lịch sử
- [ ] **Gate 2:** demo phụ đề cho khách

## Stage 7 – Lựa chọn hội thoại (M) · FR-05
- [ ] `ChoiceParser`: tách từng lựa chọn, bỏ số thứ tự và ▶
- [ ] Nhận dòng đang chọn (màu / ▶), hiển thị lại số thứ tự, ▶ và màu nổi bật
- [ ] Không dịch lại khi chỉ di chuyển lựa chọn (cache theo từng lựa chọn)

## Stage 8 – Chọn vùng thủ công (M, best effort) · FR-07
- [ ] Phím tắt → khoanh vùng → OCR + dịch một lần → popup; tắt bằng phím tắt hoặc khi vùng đổi

## Stage 9 – Hoàn thiện và nghiệm thu (M) · FR-08, FR-11 · Gate 3
- [ ] UI cài đặt (key, nhà cung cấp, cỡ chữ, màu/độ mờ nền, phím tắt, engine chụp)
- [ ] UI sửa hồ sơ game; hồ sơ Witcher 3 thật
- [ ] Đo AC-01..AC-05 bằng Benchmark, xuất bảng cho khách chấm; kiểm AC-06 thủ công
- [ ] `dotnet publish` self-contained win-x64 → zip + README cài đặt
- [ ] **Gate 3:** đạt AC-01..AC-06

---

## Nhật ký

| Ngày | Nội dung |
|---|---|
| 2026-11-22 | Đọc tài liệu, lập kế hoạch, chốt .NET 8 và làm cả hai engine chụp |
| 2026-11-23 | Xong Stage 0 (chưa commit) |
