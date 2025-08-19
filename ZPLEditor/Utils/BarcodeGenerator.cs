using Avalonia.Media.Imaging;
using System;
using System.Linq;
using ZXing;
using ZXing.Common;

namespace ZPLEditor.Utils
{
    /// <summary>
    /// Утилиты для генерации штрих- и двумерных кодов.
    /// </summary>
    public static class BarcodeGenerator
    {
        /// <summary>
        /// Генерирует QR-код на основе переданного текста.
        /// </summary>
        /// <param name="text">Текст для кодирования.</param>
        /// <param name="width">Ширина изображения (по умолчанию 200).</param>
        /// <param name="height">Высота изображения (по умолчанию 200).</param>
        /// <returns>Изображение QR-кода в формате <see cref="Bitmap"/>.</returns>
        /// <exception cref="ArgumentException">Выбрасывается, если текст пустой или null.</exception>
        public static Bitmap GenerateQrCode(string text, int width = 200, int height = 200)
        {
            ValidateInput(text);
            var writer = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                }
            };
            return writer.Write(text);
        }

        /// <summary>
        /// Генерирует штрих-код EAN-13.
        /// </summary>
        /// <param name="ean13">13-значный EAN-код.</param>
        /// <param name="width">Ширина изображения (по умолчанию 200).</param>
        /// <param name="height">Высота изображения (по умолчанию 200).</param>
        /// <returns>Изображение EAN-13 в формате <see cref="Bitmap"/>.</returns>
        /// <exception cref="ArgumentException">Если код не является 13-значным числом.</exception>
        public static Bitmap GenerateEan13(string ean13, int width = 200, int height = 200)
        {
            ValidateInput(ean13);
            if (!IsValidEan13(ean13))
                throw new ArgumentException("EAN-13 должен содержать ровно 13 цифр.", nameof(ean13));

            var writer = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.EAN_13,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                }
            };
            return writer.Write(ean13);
        }

        /// <summary>
        /// Генерирует Data Matrix код.
        /// </summary>
        /// <param name="text">Текст для кодирования.</param>
        /// <param name="width">Ширина изображения (по умолчанию 200).</param>
        /// <param name="height">Высота изображения (по умолчанию 200).</param>
        /// <returns>Изображение Data Matrix в формате <see cref="Bitmap"/>.</returns>
        public static Bitmap GenerateDataMatrix(string text, int width = 200, int height = 200)
        {
            ValidateInput(text);
            var writer = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.DATA_MATRIX,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                }
            };
            return writer.Write(text);
        }

        /// <summary>
        /// Генерирует штрих-код Code 128, подходящий для кодирования EAN-128 (GS1-128).
        /// Внимание: данные должны быть правильно отформатированы (например, с AI).
        /// </summary>
        /// <param name="data">Данные для кодирования (включая AI, если нужно).</param>
        /// <param name="width">Ширина изображения (по умолчанию 200).</param>
        /// <param name="height">Высота изображения (по умолчанию 200).</param>
        /// <returns>Изображение Code 128 в формате <see cref="Bitmap"/>.</returns>
        public static Bitmap GenerateCode128(string data, int width = 200, int height = 200)
        {
            ValidateInput(data);
            var writer = new BarcodeWriter<Bitmap>
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                }
            };
            return writer.Write(data);
        }

        // Вспомогательные методы

        private static void ValidateInput(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Текст не может быть null или пустым.", nameof(text));
        }

        private static bool IsValidEan13(string ean)
        {
            return ean.Length == 13 && ean.All(char.IsDigit);
        }
    }
}