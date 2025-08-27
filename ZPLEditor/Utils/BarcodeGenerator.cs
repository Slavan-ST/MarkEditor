using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using ZXing;
using ZXing.Common;
using ZXing.SkiaSharp.Rendering;

namespace ZPLEditor.Utils
{
    public static class BarcodeGenerator
    {
        // Базовый метод остаётся тем же
        private static Bitmap GenerateBarcode(Action<BarcodeWriter<SKBitmap>> configureWriter, string value)
        {
            var writer = new BarcodeWriter<SKBitmap>
            {
                Renderer = new ZXing.SkiaSharp.Rendering.SKBitmapRenderer()
            };

            configureWriter(writer);

            using var skBitmap = writer.Write(value);
            using var image = SKImage.FromBitmap(skBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;

            return new Bitmap(stream);
        }

        // QR-код: минимум 25×25 мм → 300×300 px при 304 DPI
        public static Bitmap GenerateQrCode(string text, int width = 300, int height = 300)
        {
            ValidateInput(text);
            return GenerateBarcode(writer =>
            {
                writer.Format = BarcodeFormat.QR_CODE;
                writer.Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2,
                    // Для QR — можно добавить коррекцию ошибок
                    PureBarcode = false
                };
            }, text);
        }

        // EAN-13: типичная этикетка 38×25 мм → 456×300 px
        public static Bitmap GenerateEan13(string ean13, int width = 460, int height = 300)
        {
            ValidateInput(ean13);
            return GenerateBarcode(writer =>
            {
                writer.Format = BarcodeFormat.EAN_13;
                writer.Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 10 // важно: слишком малый margin — ошибка сканирования
                };
            }, ean13);
        }

        // Data Matrix: минимум 10×10 мм → 120×120 px, но лучше 200×200
        public static Bitmap GenerateDataMatrix(string text, int width = 200, int height = 200)
        {
            ValidateInput(text);
            return GenerateBarcode(writer =>
            {
                writer.Format = BarcodeFormat.DATA_MATRIX;
                writer.Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                };
            }, text);
        }

        // Code 128 — часто используется для EAN-128 / GS1-128
        // Ширина зависит от длины данных. Для 20 символов — ~800 px при 304 DPI
        public static Bitmap GenerateCode128(string data, int? width = 800, int height = 150)
        {
            ValidateInput(data);

            // Эмпирическая формула: ~40 px на символ, минимум 800
            int calculatedWidth = width ?? Math.Max(800, data.Length * 40);
            calculatedWidth = Math.Max(calculatedWidth, 800); // минимум

            return GenerateBarcode(writer =>
            {
                writer.Format = BarcodeFormat.CODE_128;
                writer.Options = new EncodingOptions
                {
                    Width = calculatedWidth,
                    Height = height,
                    Margin = 10
                };
            }, data);
        }


        private static void ValidateInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Текст не может быть null или пустым.", nameof(text));
        }
    }
}
