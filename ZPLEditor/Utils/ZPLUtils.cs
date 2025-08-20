using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ZPLEditor.ViewModels;

namespace ZPLEditor.Utils
{
    /// <summary>
    /// Вспомогательный класс для генерации ZPL-кода из элементов интерфейса и отправки его на принтер.
    /// </summary>
    public static class ZPLUtils
    {
        // Конфигурация принтера
        private const string PrinterIp = "192.168.10.202";
        private const int PrinterPort = 9100;
        private const int Dpi = 304; // Типичное разрешение для промышленных принтеров Zebra
        private const int SourceDpi = 96; // Типичное разрешение для avalonia

        #region Генерация ZPL из элементов холста

        public static string GenerateZplFromCanvas(Canvas canvas)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            var zplElements = new List<ZplElementBase>();

            ProcessCanvas(canvas, zplElements);

            //CanvasExporter.SaveCanvasToFile(canvas, "C:\\temp\\debug.png"); // тест изображения
            return BuildZplString(zplElements);
        }



        private static void ProcessCanvas(Canvas canvas, List<ZplElementBase> zplElements)
        {
            const double UiDpi = 96;

            // Явно заданные размеры в дюймах
            var widthInches = canvas.Width / UiDpi;
            var heightInches = canvas.Height / UiDpi;

            // Целевой размер в пикселях
            var pixelWidth = (int)Math.Ceiling(widthInches * Dpi);
            var pixelHeight = (int)Math.Ceiling(heightInches * Dpi);

            var pixelSize = new PixelSize(pixelWidth, pixelHeight);
            var dpi = new Vector(96, 96);

            using var bitmap = new RenderTargetBitmap(pixelSize, dpi);

            using (var context = bitmap.CreateDrawingContext())
            {
                var scaleX = Dpi / UiDpi;
                var scaleY = Dpi / UiDpi;

                context.PushPostTransform(Matrix.CreateScale(scaleX, scaleY));
                var brush = new VisualBrush(canvas);
                context.DrawRectangle(brush, null, new Rect(0, 0, canvas.Width, canvas.Height));
                //context.Pop();
            }

            var data = RenderTargetBitmapToByteArray(bitmap);

            try
            {
                zplElements.Add(new ZplDownloadGraphics('R', "label", data));
                zplElements.Add(new ZplRecallGraphic(0, 0, 'R', "label")); // Позиция 0,0
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        public static byte[] RenderTargetBitmapToByteArray(RenderTargetBitmap bitmap)
        {
            using var stream = new MemoryStream();
            bitmap.Save(stream); // Сохраняет как PNG
            return stream.ToArray();
        }

        private static string BuildZplString(List<ZplElementBase> zplElements)
        {
            var engine = new ZplEngine(zplElements);
            var options = new ZplRenderOptions
            {
                AddEmptyLineBeforeElementStart = true,
                TargetPrintDpi = Dpi,
                SourcePrintDpi = Dpi
            };

            var zpl = engine.ToZplString(options);
            return zpl;
        }

        #endregion

        #region Печать через TCP

        /// <summary>
        /// Асинхронно отправляет ZPL-код на принтер по TCP.
        /// </summary>
        /// <param name="zpl">ZPL-команды в виде строки</param>
        /// <returns>Задача, представляющая операцию отправки</returns>
        public static async Task PrintZPLAsync(string zpl)
        {
            if (string.IsNullOrWhiteSpace(zpl))
                throw new ArgumentException("ZPL не может быть пустым.", nameof(zpl));

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(PrinterIp, PrinterPort);

                using var stream = client.GetStream();
                var bytes = Encoding.UTF8.GetBytes(zpl);
                await stream.WriteAsync(bytes, 0, bytes.Length);

                Debug.WriteLine("ZPL успешно отправлен на принтер.");
            }
            catch (SocketException sockEx)
            {
                Debug.WriteLine($"Ошибка подключения к принтеру ({PrinterIp}:{PrinterPort}): {sockEx.Message}");
                throw new InvalidOperationException("Не удалось подключиться к принтеру.", sockEx);
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"Ошибка при отправке данных принтеру: {ioEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Неизвестная ошибка при печати: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Синхронная версия печати (устаревшая, но может использоваться при необходимости).
        /// Рекомендуется использовать <see cref="PrintZPLAsync(string)"/>.
        /// </summary>
        /// <param name="zpl">ZPL-команды</param>
        public static void PrintZPL(string zpl)
        {
            Task.Run(async () => await PrintZPLAsync(zpl)).Wait();
        }

        #endregion

        #region Вспомогательные методы (опционально)

        /// <summary>
        /// Проверяет доступность принтера.
        /// </summary>
        /// <returns>true, если принтер отвечает</returns>
        public static async Task<bool> IsPrinterAvailableAsync()
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(PrinterIp, PrinterPort);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}