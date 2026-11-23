# Bộ mẫu Witcher 3

- `images/`: ảnh gốc chụp từ cửa sổ nguồn (không đưa lên git vì nặng). Hiện là 10 ảnh từ video YouTube, chỉ để benchmark sơ bộ; bộ nghiệm thu chính thức phải chụp từ Chiaki.
- `ground-truth.csv`: bản chép chuẩn, dùng để chấm OCR (AC-01, AC-03).

## Quy ước chép `ground-truth.csv`

| Cột | Ý nghĩa |
|---|---|
| `image` | Tên file trong `images/` |
| `region` | `subtitle` hoặc `choices` (giống `type` trong hồ sơ game) |
| `text` | Chữ đúng như trên màn hình |

- Chép đúng từng ký tự: chữ hoa/thường, dấu câu, dấu nháy, `...`, `-`, `[ ]`.
- Phụ đề nhiều dòng: nối thành một dòng, cách nhau một dấu cách.
- Tên người nói hiện trên màn hình (vd `Ciri:`) thì chép luôn.
- Lựa chọn hội thoại: giữ số thứ tự (`1.`), bỏ ký hiệu `▶`; các lựa chọn nối bằng `|`.
- Vùng không có chữ thì không ghi dòng. Chữ HUD (hướng dẫn phím…) không chép.
