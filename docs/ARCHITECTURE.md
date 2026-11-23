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