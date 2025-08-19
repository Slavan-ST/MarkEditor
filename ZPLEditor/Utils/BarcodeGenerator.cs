using Avalonia.Media.Imaging;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using ZXing;
using ZXing.Common;

namespace ZPLEditor.Utils
{
    public static class BarcodeGenerator
    {
        // Общий метод генерации штрихкода
        private static Bitmap GenerateBarcode(Action<BarcodeWriter<SKBitmap>> configureWriter, string value)
        {
            // Создаём BarcodeWriter, который генерирует SKBitmap
            var writer = new BarcodeWriter<SKBitmap>
            {
                Renderer = new ZXing.SkiaSharp.Rendering.SKBitmapRenderer()
            };

            configureWriter(writer);

            // Генерируем SKBitmap
            using var skBitmap = writer.Write(value);

            // Конвертируем SKBitmap → SKImage → PNG → Stream → Avalonia Bitmap
            using var image = SKImage.FromBitmap(skBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream();
            data.SaveTo(stream);
            stream.Position = 0;

            return new Bitmap(stream);
        }

        // Пример: QR-код
        public static Bitmap GenerateQrCode(string text, int width = 200, int height = 200)
        {
            ValidateInput(text);
            return GenerateBarcode(writer =>
            {
                writer.Format = BarcodeFormat.QR_CODE;
                writer.Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                };
            }, text);
        }

        // Пример: EAN-13
        public static Bitmap GenerateEan13(string ean13, int width = 200, int height = 200)
        {
            ValidateInput(ean13);
            if (!IsValidEan13(ean13))
                throw new ArgumentException("EAN-13 должен содержать ровно 13 цифр.", nameof(ean13));

            return GenerateBarcode(writer =>
            {
                writer.Format = BarcodeFormat.EAN_13;
                writer.Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                };
            }, ean13);
        }

        // Data Matrix
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

        // Code 128 (для EAN-128 / GS1-128)
        public static Bitmap GenerateCode128(string data, int width = 200, int height = 200)
        {
            ValidateInput(data);
            return GenerateBarcode(writer =>
            {
                writer.Format = BarcodeFormat.CODE_128;
                writer.Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                };
            }, data);
        }

        // Вспомогательные методы
        private static void ValidateInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Текст не может быть null или пустым.", nameof(text));
        }

        private static bool IsValidEan13(string ean) =>
            ean.Length == 13 && ean.All(char.IsDigit);
    }
}