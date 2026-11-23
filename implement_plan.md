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

## Stage 1 – Chụp và chọn vùng (M) · FR-01, FR-02 · 🟡 code xong 2026-11-23, chờ thử tay
- [x] `WindowFinder`: liệt kê cửa sổ (bỏ cửa sổ ẩn/cloaked/tool window/của chính app), tìm theo `windowTitleHint`
- [x] Nhớ cửa sổ theo hồ sơ: chọn cửa sổ mà gợi ý cũ không còn khớp thì ghi tiêu đề mới vào `windowTitleHint`
- [x] `WindowsGraphicsCapture` → `Frame`: giữ một phiên/cửa sổ, luôn giữ khung mới nhất, chép về CPU khi gọi
- [x] `DesktopDuplicationCapture` → `Frame`: nhân bản màn hình chứa cửa sổ, cắt theo DWM extended frame bounds; tạo lại khi AccessLost
- [x] Chọn engine chụp trên cửa sổ chính, lưu vào `settings.json`
- [x] `RegionSelectorWindow`: kéo chuột trên ảnh chụp, lưu tọa độ 0–1 vào hồ sơ (giữ textColor, selectedMarker…)
- [x] Nút "Lưu PNG debug": ảnh đầy đủ + ảnh từng vùng vào `%AppData%\AppTranslator\debug`
- [x] `Region.ToPixelRect` / `WithPixelRect` + 6 unit test
- **Đã kiểm:** build 0 warning; 10/10 test; harness chụp cửa sổ VS Code: WGC lần đầu ~200ms, các lần sau ~3ms; DXGI ~38ms / ~2.5ms; cùng kích thước 1920x1040, WGC chụp đúng cả khi bị che
- [x] Thử tay bằng video YouTube Witcher 3 (thay Chiaki): 21 lần chụp, chọn vùng phụ đề + lựa chọn hội thoại hoạt động, cắt vùng đúng
- [x] Sửa: phiên WGC không dừng khi đổi engine/cửa sổ → viền vàng lọt vào ảnh DXGI. Giờ `WindowsGraphicsCapture.Stop()` khi đổi
- [ ] 1 ảnh WGC đen hoàn toàn (18:04:35) chưa rõ nguyên nhân, chưa tái hiện được
- [ ] Chụp cửa sổ Chiaki thật (khi có)
- 10 ảnh từ video (sáng/tối/lựa chọn/2 dòng) chép vào `samples/witcher3/images/video-*.png` để benchmark sơ bộ Stage 3; thư mục `images/` không đưa lên git
- Ghi chú: `ProfileVersion` không tăng khi đổi vùng/cửa sổ vì không ảnh hưởng bản dịch trong cache

## Stage 2 – Overlay và phím tắt (M) · FR-03, FR-06
- [ ] Overlay WPF trong suốt: `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`, TOPMOST
- [ ] `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` cho overlay (bắt buộc với DXGI, nếu không overlay lọt vào ảnh chụp)
- [ ] Manifest PerMonitorV2 DPI để vị trí overlay khớp pixel vật lý
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
| 2026-11-23 | Xong Stage 0, commit `ff25fd8` lên nhánh `stage_0` (origin: github.com/lekhatiep/AppTranslator) |
| 2026-11-23 | Tạo `main` từ `stage_0`; Stage 1 code xong trên nhánh `stage_1`. Sửa deadlock WGC (FrameArrived chờ khóa khi WGC giữ khóa D3D) |
