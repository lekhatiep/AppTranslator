# AppTranslator – Hướng dẫn cài đặt và sử dụng

AppTranslator đọc phụ đề và lựa chọn hội thoại tiếng Anh trên màn hình game (ví dụ The Witcher 3 chơi qua Chiaki), dịch sang tiếng Việt bằng Gemini và hiện bản dịch đè lên đúng chỗ chữ gốc.

## 1. Yêu cầu

- Windows 10 bản 2004 trở lên, hoặc Windows 11, 64-bit.
- Gói nhận dạng chữ tiếng Anh của Windows: **Settings > Time & Language > Language**, thêm **English (United States)** và bật **Optical character recognition**. Thiếu gói này thì app báo lỗi khi bật dịch.
- Có Internet (để dịch).
- Key Gemini: tạo miễn phí tại https://aistudio.google.com/apikey. Key miễn phí dịch được khoảng 15 câu mỗi phút; đoạn hội thoại dồn dập có thể báo "Hết lượt dịch".

Không cần cài thêm .NET hay phần mềm nào khác.

## 2. Cài đặt

1. Giải nén file `AppTranslator-<ngày>.zip` vào một thư mục, ví dụ `C:\AppTranslator`.
2. Chạy `AppTranslator.App.exe`.
3. Nếu Windows hiện "Windows protected your PC": bấm **More info** rồi **Run anyway** (app chưa được ký số).

## 3. Lần dùng đầu

1. Mở game (Chiaki) trước, để ở chế độ cửa sổ không viền hoặc cửa sổ thường.
2. Trên cửa sổ AppTranslator:
   - **Hồ sơ game**: chọn *The Witcher 3: Wild Hunt*.
   - **Cửa sổ nguồn**: chọn cửa sổ Chiaki (bấm *Làm mới* nếu chưa thấy).
   - **Cách chụp**: xem mục 5.
3. Bấm **Chọn vùng chữ**, kéo chuột khoanh **vùng phụ đề** (xanh) và **vùng lựa chọn hội thoại** (vàng), rồi bấm **Lưu**. Nên chụp lúc màn hình đang có phụ đề và bảng lựa chọn để khoanh cho chuẩn.
4. **Bộ dịch**: chọn *Gemini*, dán key vào ô **Key Gemini**, bấm **Lưu key**. Key được mã hóa theo tài khoản Windows của bạn.
5. Tick **Bật dịch**. Bản dịch hiện đè lên phụ đề và bảng lựa chọn khi cửa sổ game đang được dùng.

## 4. Phím tắt (đổi được trong Cài đặt)

| Phím | Việc |
|---|---|
| `Ctrl+Alt+T` | Bật/tắt dịch |
| `Ctrl+Alt+R` | Chọn lại vùng phụ đề / lựa chọn |
| `Ctrl+Alt+Q` | Khoanh một vùng bất kỳ (mô tả vật phẩm, nhật ký…) để dịch một lần; bấm lại để đóng |

## 5. Hai cách chụp

| Cách chụp | Ưu điểm | Nhược điểm |
|---|---|---|
| WindowsGraphicsCapture | Chụp được cả khi cửa sổ game bị che; click chuột xuyên qua chữ dịch | Windows 10 hiện viền vàng quanh cửa sổ game |
| DesktopDuplication | Không có viền vàng | Cửa sổ game phải nằm trên cùng; click đúng vào ô chữ dịch sẽ không tới game (click chỗ khác vẫn bình thường) |

Chơi bằng tay cầm thì cả hai cách đều điều khiển game bình thường.

## 6. Các chức năng khác

- **Cài đặt**: cỡ chữ, màu và độ mờ nền chữ dịch, phím tắt, model Gemini.
- **Sửa / Tạo mới** (cạnh ô hồ sơ): văn phong, cách xưng hô, tên riêng giữ nguyên, bảng thuật ngữ dịch cố định (mỗi dòng `tiếng Anh = tiếng Việt`). Ví dụ thêm `Ghouls and Alghouls = Ghoul và Alghoul` để tên sách luôn dịch giống nhau.
- **Xóa cache hồ sơ**: bỏ các bản dịch đã lưu của game, để dịch lại từ đầu.
- **Lịch sử dịch**: xem 200 câu gần nhất và độ trễ.

## 7. Sự cố thường gặp

| Hiện tượng | Cách xử lý |
|---|---|
| Báo thiếu OCR tiếng Anh | Cài gói ngôn ngữ ở mục 1 rồi mở lại app |
| Overlay không hiện | Tick *Bật dịch*; cửa sổ game phải đang được dùng (không thu nhỏ, đang ở trên) |
| Chữ dịch lệch chỗ hoặc đọc sai nhiều | Bấm *Chọn vùng chữ* khoanh lại sát vùng chữ; vùng không nên lẹm vào thanh taskbar hay chữ khác |
| "⚠ Hết lượt dịch Gemini" | Key miễn phí đã chạm giới hạn; chờ khoảng 1 phút, hoặc dùng key trả phí |
| "⚠ Key Gemini sai hoặc không có quyền" | Dán lại key và bấm *Lưu key* |
| Cần báo lỗi cho nhà phát triển | Bấm *Lưu PNG debug* lúc đang lỗi, gửi kèm giờ xảy ra và thư mục `logs` (mục 8) |

## 8. Dữ liệu và gỡ cài đặt

Cài đặt, hồ sơ game, key (đã mã hóa), cache, lịch sử và log nằm trong `%AppData%\AppTranslator` (dán đường dẫn này vào thanh địa chỉ File Explorer để mở). App không gửi ảnh chụp màn hình đi đâu; chỉ gửi câu chữ cần dịch tới Gemini.

Gỡ cài đặt: xóa thư mục đã giải nén, và xóa `%AppData%\AppTranslator` nếu muốn xóa luôn dữ liệu.
