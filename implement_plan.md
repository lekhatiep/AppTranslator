# Implement Plan – AppTranslator Phase 1

Nguồn yêu cầu: [docs/DocApptTranslator.docx](docs/DocApptTranslator.docx) (FR-01..FR-13, AC-01..AC-06).
Đánh dấu: `[ ]` chưa làm · `[~]` đang làm · `[x]` xong · `[!]` bị chặn (ghi lý do).

## Quyết định đã chốt

| Ngày | Quyết định | Ghi chú |
|---|---|---|
| 2026-11-22 | Runtime: **.NET 8** (`net8.0-windows10.0.19041.0`) | Build bằng SDK 9 có sẵn trên máy. .NET 8 hết hỗ trợ 10/11/2026, nâng lên .NET 10 khi cần |
| 2026-11-22 | Chụp màn hình: làm **cả hai** engine sau `IScreenCapture` | (a) Windows.Graphics.Capture: chụp đúng cửa sổ, nhưng trên Win10 có viền vàng. (b) DXGI Desktop Duplication: không viền, overlay loại khỏi ảnh bằng `WDA_EXCLUDEFROMCAPTURE`, không chụp được khi cửa sổ bị che. Chọn engine trong cài đặt |
| 2026-11-22 | Chưa có key Gemini | Stage 5 làm trước bằng `MockTranslator`, khi có key thì nối Gemini thật |
| 2026-11-23 | Nhánh: mỗi stage tách `stage_<N>` từ `development` | |
| 2026-11-23 | Gate 1 tạm: phụ đề Windows OCR + Sharpen (83.2%), lựa chọn PaddleOCR v5 kèm gói (98.2%) | Đo trên 14 ảnh video YouTube. Phụ đề chưa đạt 95% (AC-01): thử đọc nhiều khung lấy đa số + ghép 2 engine ở Stage 4. Chốt lại với bộ ảnh Chiaki |
| 2026-11-24 | Giữ Gate 1 tạm, chưa đổi sang Ensemble (91%) | Khách ưu tiên chạy được trọn luồng trước, chấp nhận dịch sai một phần; Ensemble làm ở giai đoạn bảo trì |
| 2026-11-23 | Overlay hai chế độ theo engine (hướng 1) | WGC: click-through hoàn toàn. DXGI: không lọt vào ảnh chụp, click trúng ô chữ bị overlay nhận (không tới game), click chỗ khác bình thường. Click-through cho DXGI để giai đoạn bảo trì |

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

## Stage 2 – Overlay và phím tắt (M) · FR-03, FR-06 · ✅ xong 2026-11-23 (còn thử với Chiaki + tay cầm)
- [x] Overlay hai chế độ theo engine chụp (quyết định 2026-11-23, xem “Quyết định đã chốt”):
  - WGC: `AllowsTransparency` + `WS_EX_TRANSPARENT | NOACTIVATE | TOOLWINDOW`, TOPMOST → click-through hoàn toàn
  - DXGI: cửa sổ thường, nền trong suốt bằng DWM glass, `WDA_EXCLUDEFROMCAPTURE`, chặn `WM_MOUSEACTIVATE`, `SetWindowRgn` chỉ giữ các ô chữ
- [x] Manifest PerMonitorV2 DPI; overlay đặt bằng `SetWindowPos` pixel vật lý, ô chữ theo tỉ lệ vùng
- [x] Bám cửa sổ nguồn (`SetWinEventHook`: LOCATIONCHANGE/DESTROY của tiến trình nguồn, FOREGROUND/MINIMIZE toàn hệ thống); chỉ hiện khi cửa sổ nguồn đang foreground và không thu nhỏ
- [x] Phím tắt toàn cục (`RegisterHotKey` trên cửa sổ message-only): `Ctrl+Alt+T` bật/tắt dịch, `Ctrl+Alt+R` chọn vùng; đổi trong `settings.json` (UI ở Stage 9); `HotkeyGesture.TryParse` + 15 unit test
- [x] Checkbox “Bật dịch” trên cửa sổ chính; overlay hiện chữ giả (`[Thử]`) theo vùng của hồ sơ
- **Đã kiểm (harness với Notepad, click chuột thật):** trùng khung từng pixel, bám khi di chuyển/đổi cỡ, ẩn khi thu nhỏ/tắt dịch/đóng nguồn, không giành focus, phím tắt hoạt động khi nguồn đang foreground; WGC click-through cả trong ô chữ; DXGI: overlay không lọt vào ảnh DXGI, click ngoài ô chữ vào nguồn, region đúng bằng các ô chữ
- [x] Thử tay trên video YouTube ở cả hai chế độ: ô chữ đúng vùng, ngoài ô trong suốt, tiếng Việt hiển thị đúng; DXGI xác nhận overlay không có trong ảnh chụp màn hình (phải chụp bằng điện thoại)
- [x] Độ mờ nền mặc định 0.6 → 0.9: ở 0.6 phụ đề tiếng Anh gốc lộ qua nền, chồng lên chữ dịch
- [ ] Tay cầm với Chiaki khi có (AC-06)
- Đã đo: overlay WPF trên Win10 không thể vừa click-through vừa `WDA_EXCLUDEFROMCAPTURE` (`AllowsTransparency` → lỗi 8; thêm WS_EX_LAYERED vào cửa sổ thường → `SetLayeredWindowAttributes` lỗi 87, mất click-through)

## Stage 3 – Benchmark OCR (M) · Gate 1 · ✅ xong 2026-11-23 (Gate 1 tạm, chốt lại với ảnh Chiaki)
- [x] `samples/witcher3/ground-truth.csv` (16 dòng, 10 ảnh video, người dùng đã soát 2026-11-23) + README quy ước chép
- [x] `TextAccuracy` (tỉ lệ từ đúng theo Levenshtein, chế độ chặt/lỏng) + 10 unit test
- [x] Imaging: `FrameConverter` (Frame↔Mat, đọc/ghi/cắt/thu nhỏ), `TextPreprocessor` (None, Upscale2x, TextColorMask2x)
- [x] Ocr: `WindowsOcrEngine`, `RapidOcrEngine` (PaddleOCR PP-OCRv5 Latin qua RapidOcrNet 4.2.0, ONNX Runtime CPU; model chép theo project tham chiếu)
- [x] tools/AppTranslator.Benchmark: engine × tiền xử lý × dòng, xuất `results/<thời gian>/results.csv` + ảnh sau tiền xử lý, in bảng tổng hợp
- [ ] tests/AppTranslator.Ocr.Tests: dời đến khi có ảnh mẫu đưa vào git được (ảnh hiện không lên git)
- Kết quả lần 1 (2026-11-23, vùng hồ sơ hiện tại, “chặt” = so đúng dấu câu):

  | Engine | Tiền xử lý | Phụ đề | Lựa chọn | ms/vùng |
  |---|---|---|---|---|
  | WindowsOcr | None | 80.1% | 90.9% | ~7 |
  | WindowsOcr | Upscale2x | 78.3% | 80.9% | ~20 |
  | WindowsOcr | TextColorMask2x | 71.5% | 77.3% | ~15 |
  | PaddleOcrV5 | None | 47.0% | 97.5% | ~140 |
  | PaddleOcrV5 | Upscale2x | 62.5% | 94.6% | ~2000 |
  | PaddleOcrV5 | TextColorMask2x | 67.7% | 88.4% | ~300 |

  - 2/10 phụ đề sai do **vùng**, không do OCR: 181007 mất dòng 2 (ảnh cao 1056 nên tỉ lệ lệch), 181336 phụ đề lúc chơi nằm cao hơn vùng (chỉ đọc được HUD)
  - Lỗi OCR còn lại: dính chữ (“ouryoung”), dấu câu rác; Paddle + tách màu đọc nền thành ký tự rác
  - RapidOcrNet mặc định thu cạnh dài về 1024px → phụ đề chỉ 15%; đã nâng lên 4096
- [x] Lần 2: thêm 4 ảnh toàn màn hình (14 ảnh, 22 dòng), vùng phụ đề cao hơn (y 866–1034): Windows 81.3% / 89.4%, Paddle 79.4% / 98.2% (phụ đề / lựa chọn). Lỗi phổ biến nhất: mất dấu cách giữa từ
- [x] Lần 3: thử thêm tiền xử lý và model Paddle lớn
  - Tiền xử lý: **Sharpen** tốt nhất cho Windows (phụ đề 83.2%, 9/14 câu đúng trọn); Grayscale ≈ không đổi, Upscale1.5x kém hơn → bỏ, chỉ giữ Sharpen
  - Model Paddle (en_rec mobile, rec/det server): đều kém hơn bản kèm gói (3–38% phụ đề); rec server là model Trung+Anh nên bỏ dấu cách giữa từ, chậm 3–15 s/vùng → đã xóa model và code thử nghiệm (theo yêu cầu: dưới 95% thì xóa)
- [x] Vùng phụ đề nâng lên y 801–1053 (bắt cả phụ đề lúc chơi). Lần 4: Windows + Sharpen 81.9% (7/14); 181336 đã vào vùng nhưng OCR sai trên nền gỗ sáng và lẫn chữ HUD; Paddle giảm còn 69.6% vì thêm nền nhiễu
- [ ] (Tùy chọn) Tesseract làm mốc so sánh; DirectML cho Paddle nếu cần tốc độ
- [x] **Gate 1 (tạm, 2026-11-23):** phụ đề = Windows OCR + Sharpen; lựa chọn = PaddleOCR v5 (kèm gói), giữ nguyên ảnh. Chốt lại khi có ảnh Chiaki thật

## Stage 4 – Nhận diện phụ đề (L) · FR-04 · 🟡 đang làm (nhánh `stage_4`)
- [x] `SentenceAssembler` (gộp mảnh cùng hàng, xếp trên→dưới, **bỏ hàng lệch tâm >15% = chữ HUD**) + 6 test; đưa vào benchmark: Paddle+Sharpen phụ đề 70.6% → 81.4%, Windows+Sharpen 82.6%
- [x] `OcrVote.SelectMedian` + 6 test; `WordDistance` dùng chung với `TextAccuracy`
- [x] Benchmark “Ensemble” = OcrVote trên 5 cấu hình (Windows None/Sharpen/Upscale2x + Paddle None/Sharpen): **phụ đề 91.0%, lựa chọn 100%**, ~800 ms nếu chạy lần lượt. Lưu ý: tổ hợp chọn trên chính 14 ảnh này nên có thể lạc quan. **Chưa dùng trong app** (để bảo trì, xem dưới)
- [x] Đo CPU (i7-12700F, 20 luồng): PaddleOCR mặc định chiếm ~94% cả máy mỗi lần đọc (6.3 s CPU). Giới hạn ONNX Runtime 4 luồng + tắt spin: 177 ms thay vì 338 ms, 0.47 s CPU, kết quả giống hệt → đã áp dụng. Ensemble trên 14 ảnh: 420 ms/câu, ~1.07 s CPU/câu (≈13% máy trong ~0.4 s) ≈ 1.8% CPU trung bình nếu 3 s có một câu mới
- [x] `TextMask` (thay cho `ChangeDetector`): ô 8×8 px có ≥2 điểm sáng (độ sáng ≥200) là ô chữ, `ChangeRatio` = ô khác / ô có chữ. Trên 14 ảnh mẫu: ~1 ms/khung; cùng câu nén JPEG 60 lệch ≤0.19, hai câu khác nhau ≥0.44 → ngưỡng 0.3
- [x] `StabilityGate`: báo Changed / Stable (đứng yên 300 ms, một lần) / Cleared (vùng trống đứng yên sau khi có câu)
- [x] Bỏ phiếu nhiều khung: OCR 3 khung cuối của lần đứng yên (cách 100 ms), `OcrVote.SelectMedian`
- [x] `SubtitleReader` (App): chụp 10 khung/giây trên luồng UI, cắt + mặt nạ + làm nét chạy nền, Windows OCR + Sharpen (Gate 1 tạm); hiện câu và độ trễ trên cửa sổ chính, ghi log. Chạy khi bật dịch, khởi động lại khi đổi hồ sơ/cửa sổ/engine
- [x] Unit test: `TextMask` 6, `StabilityGate` 5 (tổng 58)
- [x] Phát lại 14 ảnh mẫu như video (mỗi ảnh 1.5 s, xen khoảng trống) qua `SubtitleReader` thật: đọc 14/14 câu, mỗi câu một lần, 14 lần tắt; trễ 340–510 ms (gồm 300 ms chờ), OCR 37–100 ms; CPU 0.6% máy (chưa tính chép khung từ GPU)
- [x] Công cụ `tools/AppTranslator.VideoReplay`: phát lại video (mp4 trong `samples/<game>/videos/`, không commit) qua `SubtitleReader` theo thời gian thật, ghi log từng khung + ảnh cắt mỗi lần đọc
- [x] Sửa lỗi đọc lại liên tục (video 24/11, lúc có bảng lựa chọn): nguyên nhân không phải vùng lựa chọn mà là nền sáng chuyển động (áo, tường đá, nhân vật thở) và con trỏ chuột trong vùng phụ đề làm mặt nạ trôi quá ngưỡng → OCR lại, mỗi lần ra biến thể khác. Thêm `SentenceComparer` (khác ≤15% ký tự chữ/số = cùng câu) + 9 test: câu trùng câu đang hiện thì bỏ qua; quên câu cũ khi phụ đề tắt. Video: 16 → 1 lần đọc (15 lần bỏ qua); 14 ảnh mẫu vẫn đọc đủ 14 câu
  - Kho phiếu theo câu (ảnh 131744 + log app 24/11): lần đọc đầu hay sai hơn các lần đọc lại ("ÄJgh", "(töthe"), nên mọi lần đọc của cùng câu được gom (tối đa 30 kết quả) và chọn lại kết quả ở giữa; chỉ sửa câu đang hiện khi bản mới gần các phiếu hơn ≥0.5 từ/phiếu (chống nhảy "But you"/"Butyou"). Mô phỏng trên log: sửa đúng nhiều câu ("Ugh. I'm sorry…", "Shall we run the walls?", "But this has to end. Now."), số lần sửa 23 → 11 nhờ ngưỡng. Video: đọc sai lần đầu rồi sửa 1 lần đúng. Stage 5 cần lưu ý: mỗi lần sửa = dịch lại câu đó
  - Thêm điều kiện: câu sửa phải có ≥2 lần đọc giống hệt (log 13:42: câu "John of Brugge…" trên nền sáng bị sửa qua lại giữa các bản sai khác kiểu). Mô phỏng log cả ngày: 21 → 10 lần sửa, cả 10 đều tốt hơn
  - Đã thử mặt nạ chặt hơn (điểm sáng có viền tối): giảm OCR lại 21 → 6 lần nhưng không hết, chưa áp dụng để giữ các ngưỡng đã đo
- [ ] Chạy thử trên cửa sổ thật (video Witcher 3 trong Chrome hoặc Chiaki): câu đúng, không đọc lặp, đo CPU cả vòng chụp
- Giới hạn: bộ mẫu chỉ có 1 khung/cảnh nên chưa đo được lợi ích của bỏ phiếu nhiều khung; kiểm trên video trực tiếp
- **Xong khi:** ra đúng câu tiếng Anh từ ảnh mẫu và cửa sổ thật

## Stage 5 – Dịch (M) · FR-09, FR-10, FR-13 · 🟡 đang làm (nhánh `stage_5`)
- [x] `MockTranslator` (dùng khi chưa có key; không ghi vào cache)
- [x] `PromptBuilder`: văn phong, xưng hô, `keepNames`, glossary, N câu trước (EN + VI); dặn AI câu nguồn là OCR có thể sai nhẹ, tự đoán ý đúng
- [x] `GeminiTranslator` (REST generateContent, key qua header `x-goog-api-key`, timeout 8 s); test bằng phản hồi HTTP giả. Model để trong settings (`geminiModel`, tạm `gemini-3.5-flash-lite`) *(chạy thật khi có key)*
- [x] Lưu key bằng DPAPI (`SecretStore`, `secrets.bin`, theo tài khoản Windows), không ghi ra log
- [x] SQLite (`SqliteTranslationCache`, `data\translator.db`): khóa (ProfileId, SHA-256 câu đã chuẩn hóa, ProfileVersion) nên "Butyou were" và "But you were" dùng chung bản dịch; đếm HitCount; xóa cache theo hồ sơ
- [x] Lỗi → `TranslationException` có loại: key sai (400 API_KEY_INVALID/401/403), hết lượt (429), quá chậm, mất mạng, Gemini lỗi (5xx), khác; câu báo tiếng Việt ngắn
- [x] `TranslationService`: bỏ câu rác (`OcrQuality`: <70% từ "giống từ" vì có số/ký tự lạ) → trùng câu ngay trước thì dùng lại → cache → gọi AI kèm ngữ cảnh; đổi hồ sơ thì xóa ngữ cảnh
- [x] Test: 26 test mới (tổng 72 trên nhánh này)
- Giới hạn: lọc rác không bắt được rác toàn chữ cái ("uldll", "duölegsla ee ge") vì không dùng từ điển
- [x] Nối vào app (`SubtitleTranslator`): câu đọc được → dịch → hiện trên cửa sổ chính và overlay vùng phụ đề (thay chữ mẫu Stage 2); phụ đề tắt thì ẩn. Chỉ hiện kết quả của câu mới nhất. Câu sửa khác <3 từ so với câu đã dịch thì không dịch lại
- [x] Cửa sổ chính: chọn bộ dịch (Mock/Gemini, lưu vào settings), ô nhập key (PasswordBox → DPAPI), nút xóa cache hồ sơ, ô bản dịch + thời gian dịch và tổng độ trễ
- [x] Lỗi dịch hiện `⚠ <lý do>` trên overlay và cửa sổ chính (FR-13), vòng đọc vẫn chạy
- [ ] Chạy thử trên cửa sổ thật với bộ dịch giả
- [x] Có key free (24/11). Công cụ `tools/AppTranslator.TranslateCheck` (đọc key từ secrets.bin, không in ra): `--models` liệt kê model; dịch `samples/witcher3/translate-check.txt` (16 câu OCR thật kèm lỗi, đúng thứ tự hội thoại)
  - **Chốt `gemini-3.5-flash-lite`**: 16/16 câu, trung vị 725–860 ms, lớn nhất 1.4–1.6 s (câu đầu). Cộng ~0.4 s đọc phụ đề → tổng ~1.1–1.3 s, đạt mục tiêu 1.5 s phần lớn thời gian
  - `gemini-3.1-flash-lite` và `gemini-3.8-flash`: key free bị quá tải (timeout >8 s, 503) → không dùng
  - Gemini tự hiểu đúng hầu hết lỗi OCR ("Guessshe", "tpawake", "havetÖtell", "(töthe", "ÄJgh", "yoüyere", "Donlttry"). Sai: "Jöhn df8iügge" mất tên John of Brugge; "Ghouls and Alghouls" mỗi lần dịch một kiểu → nên thêm vào glossary; Ciri nói với Vesemir xưng "ta" → hồ sơ cần quy tắc xưng hô theo nhân vật (khách cung cấp)
  - **Giới hạn key free**: chạy liên tục ~15 câu/phút thì bị 429. Hội thoại dồn dập có thể chạm giới hạn → app báo "Hết lượt dịch" trên overlay; cache chỉ giúp câu lặp. Demo dài hoặc chơi thật nên dùng key trả phí hoặc dự phòng Google Translate
- **Xong khi:** dịch được câu mẫu, đo được độ trễ

## Stage 6 – Ghép pipeline phụ đề (M) · FR-04, FR-06, FR-12 · Gate 2 · 🟡 đang làm (nhánh `stage_6`)
- [ ] `TranslationPipeline` dùng `Channel<T>` *(xem lại: Stage 5 đã nối đọc → dịch trên luồng UI, có bỏ kết quả cũ; chỉ làm nếu đo thấy cần)*
- [x] Bố cục tiếng Việt (`SubtitleLayout`, Core, 9 test): ô dịch phủ khung chữ gốc (OCR trả khung các hàng phụ đề, hợp qua các khung bỏ phiếu), căn giữa, giữ cạnh dưới; xuống dòng theo bề rộng chữ gốc (tối thiểu nửa vùng), mở rộng lên trên tới mép trên vùng; không đủ chỗ thì co chữ tới 60%; vẫn tràn thì vượt lên trên vùng. Không biết khung chữ gốc (vd thông báo lỗi) thì neo đáy giữa vùng. Dựng thử trên ảnh mẫu: câu 3 dòng phủ kín chữ Anh, cỡ chữ 28 → 27
- [x] Truyền khung chữ gốc từ câu đọc được sang overlay (bản dịch và thông báo lỗi đè đúng chỗ chữ gốc)
- [x] `PipelineMetric` + `TranslationHistory` (`SqliteHistoryStore`, cùng translator.db, 3 test): mỗi câu dịch ghi chụp+chờ đứng yên / OCR / dịch / vẽ / tổng (từ khung đầu thấy câu tới lúc bản dịch hiện, chặt hơn AC-05 vốn tính từ lúc chữ ổn định); câu sửa lại không tính số đo. Giữ 7 ngày
- [x] Màn "Lịch sử dịch": 200 câu gần nhất, nguồn (AI/cache), OCR/dịch/tổng ms, p50/p90 độ trễ tổng (`LatencyStats`, 2 test)
- [x] Chạy thật (24/11, video Witcher 3 + Gemini free, 25 câu): phần app (chụp + chờ đứng yên + OCR + vẽ) ổn định ~350–480 ms; câu từ cache tổng ~400 ms; câu gọi Gemini tổng 1.5–3.0 s → p50 435 ms (19/25 câu từ cache vì xem lại video), p90 2110 ms. Đo trực tiếp Gemini lúc đó: 1.6 s – 42 s, có 503 "high demand"; không phải do chế độ suy nghĩ (0 token suy nghĩ). **Khách chọn giữ nguyên** (chấp nhận p90 phụ thuộc tải Gemini free); hướng khác: key trả phí, hoặc gọi song song Google Translate (mục bảo trì)
- [ ] **Gate 2:** demo phụ đề cho khách

## Stage 7 – Lựa chọn hội thoại (M) · FR-05 · 🟡 đang làm (nhánh `stage_7`)
- [x] `ChoiceParser`: tách theo hàng (dùng chung cách gộp hàng của `SentenceAssembler`), dòng bắt đầu bằng số = lựa chọn mới, bỏ ▶/rác OCR trước số, chấp nhận "1." "1)" "1,"; dòng không số = phần xuống dòng của lựa chọn trên (8 test). 8 ảnh mẫu (PaddleOCR): đúng số lựa chọn và số thứ tự ở cả 8
- [x] `SelectionMarker`: OCR không đọc ra ▶ và màu chữ không dùng được (ảnh 180920: đang chọn vàng, còn lại trắng; ảnh 224431: cả hai vàng) → đếm điểm ảnh vàng trong dải bên trái mỗi dòng, dòng nhiều nhất (≥15 điểm, gấp đôi dòng nhì) là dòng đang chọn (4 test). 8 ảnh mẫu: đều nhận ▶ ở lựa chọn 1 (đúng). *Chưa có ảnh thật ▶ ở lựa chọn 2/3*
- [x] `ReaderLoop`: một vòng chụp chung 10 khung/giây đưa khung cho mọi bộ đọc (trước mỗi bộ đọc tự chụp → chụp 2 lần/khung). `SubtitleReader` thành bộ xử lý khung; video và 14 ảnh mẫu cho kết quả như cũ
- [x] `ChoiceReader`: vùng lựa chọn đổi rồi đứng yên 300 ms → PaddleOCR (chạy nền) → `ChoiceParser` → ▶; mỗi khung dò lại ▶ (không OCR); đọc lại ra cùng các câu thì chỉ cập nhật ▶. Mặt nạ vùng lựa chọn dùng ngưỡng sáng 150 (chữ lựa chọn sáng nhất ~165; ngưỡng 200 của phụ đề làm sót 4/8 bảng ở cảnh tối). Bỏ dòng không số trước lựa chọn đầu (ảnh 224124 nền sáng ra ". N")
- [x] Phát lại 14 ảnh mẫu qua vòng chung (phụ đề + lựa chọn): phụ đề 14/14, lựa chọn 8/8 bảng đúng số lựa chọn và ▶, không bảng giả; PaddleOCR 80–370 ms/bảng; CPU 1.9% máy
- [x] Dịch từng lựa chọn song song, bộ dịch riêng (không chen vào ngữ cảnh phụ đề), cache chung theo từng câu; lưu lịch sử (loại Choices, không tính số đo độ trễ)
- [x] Overlay: bảng lựa chọn đè từ dòng lựa chọn đầu (chừa chỗ che ▶ gốc) tới mép phải vùng, mỗi dòng "▶ 1. bản dịch", dòng đang chọn tô vàng; lỗi dịch hiện ⚠ ở dòng đầu kèm chữ gốc. Cửa sổ chính có ô "Lựa chọn hội thoại (Anh → Việt)"
- [x] Không dịch lại khi chỉ di chuyển lựa chọn (chỉ vẽ lại ▶ và màu)
- [x] Ảnh thật ▶ ở lựa chọn 2 (khách chụp 25/11, nền đá sáng): nhận đúng; điểm vàng bên trái 92 so với 0 ở dòng kia (ngưỡng 15 và gấp đôi). Thêm 2 ảnh `chrome-20261125-*` vào bộ mẫu + bản chép chuẩn vùng lựa chọn
- [ ] Chạy thử trọn luồng lựa chọn trên video thật (dịch, overlay, di chuyển ▶)
- Lưu ý: mỗi bảng tốn 1 lượt gọi Gemini cho mỗi lựa chọn chưa có trong cache (key free ~15 lượt/phút)
## Stage 8 – Chọn vùng thủ công (M, best effort) · FR-07 · 🟡 đang làm (nhánh `stage_8`)
- [x] Phím tắt mới `Ctrl+Alt+Q` (`hotkeys.translateArea` trong settings.json; giữ `Ctrl+Alt+R` cho vùng của hồ sơ)
- [x] `AreaPickerWindow`: phủ ảnh chụp đứng yên lên đúng khung cửa sổ game, kéo chuột khoanh vùng, Esc hủy; kết quả theo pixel ảnh chụp
- [x] `AreaTextReader`: PaddleOCR, giữ mọi dòng (`SentenceAssembler.AssembleAllRows`, không lọc chữ lệch tâm, 1 test)
- [x] Dịch bằng bộ dịch riêng (không chen ngữ cảnh hội thoại), cache chung; lưu lịch sử loại "Manual"
- [x] `TranslationPopupWindow`: bản dịch + câu gốc nhỏ; đặt dưới vùng (thiếu chỗ thì trên), kẹp trong cửa sổ game; không giành focus, loại khỏi ảnh chụp; hiện "Đang đọc chữ…/Đang dịch…" trước, lỗi hiện ⚠. Chạy thử: vị trí dưới/trên đúng, không giành focus
- [x] Đóng popup: bấm lại phím tắt, click vào popup, hoặc chữ trong vùng đổi (kiểm 500 ms/lần, mặt nạ chữ lệch >0.3)
- [ ] Chạy thử trên game/video thật (vật phẩm, nhật ký)

## Stage 9 – Hoàn thiện và nghiệm thu (M) · FR-08, FR-11 · Gate 3 · 🟡 đang làm (nhánh `stage_9`)
- [x] UI cài đặt: nút "Cài đặt" → cỡ chữ, màu nền #RRGGBB, độ mờ (thanh trượt 0.3–1), 3 phím tắt, model Gemini; kiểm bằng `SettingsValidator` (khoảng giá trị, mã màu, phím tắt hợp lệ và không trùng; 11 test); lưu xong áp dụng ngay (đăng ký lại phím tắt, đổi model, vẽ lại overlay). Key, bộ dịch, engine chụp vẫn ở cửa sổ chính
- [x] UI hồ sơ game: nút "Sửa" / "Tạo mới" cạnh ô hồ sơ → tên game, gợi ý tiêu đề cửa sổ, văn phong, xưng hô, số câu ngữ cảnh, tên riêng (mỗi dòng một tên), thuật ngữ (mỗi dòng "Anh = Việt", dòng sai báo số dòng). `ProfileText` (9 test): đọc/ghi các ô, mã hồ sơ mới từ tên game không trùng. Lưu thì tăng Version (cache cũ của game không dùng lại)
- [ ] Hồ sơ Witcher 3 thật *(chờ khách: văn phong, xưng hô theo nhân vật, thuật ngữ; gợi ý thêm "Ghouls and Alghouls" vì Gemini dịch mỗi lần một kiểu)*
- [x] Benchmark: lựa chọn tách bằng `ChoiceParser` như app; in dòng AC-01/AC-03 theo cấu hình app. Vùng chữ ưu tiên `samples/witcher3/profiles/witcher3.json` (khớp khung hình bộ mẫu): vùng của app chọn trên cửa sổ Chrome 1920×1040 kéo tới đáy, cắt lẹm taskbar trong ảnh mẫu toàn màn hình → phụ đề tụt giả còn 58.8%
- [x] Kết quả 25/11 (16 ảnh, 24 dòng): **AC-01 phụ đề 82.6% — chưa đạt 95%** (Ensemble 91%, xem bảo trì); **AC-03 lựa chọn 98.5% — đạt**
- [x] `tools/AppTranslator.Acceptance`: `export` xuất câu đã dịch (không trùng) ra CSV có cột `danh_gia` cho khách điền Đạt/Sai + in AC-05 (chung và riêng câu gọi AI); `score` tính AC-02/AC-04 (`TranslationReview`, 10 test). Lịch sử 24–25/11: AC-05 p90 1495 ms nhưng 59/91 câu từ cache; riêng câu gọi AI p90 2075 ms (Gemini free)
- [ ] Khách chấm bảng dịch (AC-02, AC-04) trên bộ câu Chiaki; kiểm AC-06 thủ công (Chiaki Borderless, tay cầm)
- [x] `scripts/publish.ps1`: `dotnet publish` Release self-contained win-x64 → `publish/AppTranslator/` + `publish/AppTranslator-<ngày>.zip` (131.8 MB), kiểm có model OCR, hồ sơ, hướng dẫn. Bản đóng gói chạy được (có OpenCV, ONNX Runtime, SQLite, SkiaSharp native). `docs/HuongDanCaiDat.md` (kèm trong zip): yêu cầu (gói OCR tiếng Anh, key Gemini), cài, lần dùng đầu, phím tắt, hai cách chụp, sự cố thường gặp, dữ liệu/gỡ cài đặt
- [ ] Chạy thử bản zip trên máy khác (chưa có .NET) với Chiaki
- [ ] **Gate 3:** đạt AC-01..AC-06

---

## Bảo trì (sau khi xong các stage)
- [ ] Dự phòng Google Translate khi Gemini hết lượt (key free ~15 câu/phút) *(khách: để sau, 24/11)*
- [ ] Overlay chế độ DXGI click-through hoàn toàn: thử cửa sổ Win32 thuần tự vẽ (GDI hoặc Direct2D/DirectWrite) thay WPF. Thử trước ~1 giờ, nếu đạt thì viết lại ~1–1,5 ngày; ảnh hưởng phần co chữ/xuống dòng của Stage 6
- [ ] Đổi Gate 1 sang Ensemble (OcrVote trên Windows None/Sharpen/Upscale2x + Paddle None/Sharpen): phụ đề 82.6% → 91.0% trên 14 ảnh, đổi lại ~420 ms và ~1.8% CPU trung bình. Kiểm lại trên ảnh Chiaki trước khi đổi

---

## Nhật ký

| Ngày | Nội dung |
|---|---|
| 2026-11-22 | Đọc tài liệu, lập kế hoạch, chốt .NET 8 và làm cả hai engine chụp |
| 2026-11-23 | Xong Stage 0, commit `ff25fd8` lên nhánh `stage_0` (origin: github.com/lekhatiep/AppTranslator) |
| 2026-11-23 | Tạo `main` từ `stage_0`; Stage 1 code xong trên nhánh `stage_1`. Sửa deadlock WGC (FrameArrived chờ khóa khi WGC giữ khóa D3D) |
| 2026-11-23 | Stage 1 commit `2b1586d`, merge vào `development`. Stage 2 xong, commit trên `stage_2` |
| 2026-11-23 | Stage 2 merge vào `development`. Stage 3 xong (Gate 1 tạm) trên `stage_3`; đã xóa model Paddle thử nghiệm và kết quả benchmark cũ |
| 2026-11-24 | Stage 4 phần 1 (`50e14a4`) và phần 2 (vòng đọc phụ đề) trên `stage_4`; Ensemble dời sang bảo trì. Chờ chạy thử trên cửa sổ thật |
