using Avalonia;
using Avalonia.Controls;
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

        #region Генерация ZPL из элементов холста

        public static string GenerateZplFromCanvas(Canvas canvas)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            var zplElements = new List<ZplElementBase>();

            ProcessCanvas(canvas, zplElements);

            return BuildZplString(zplElements);
        }

        public static byte[] CanvasToBytes(Canvas canvas)
        {
            // элемент уже отрисован (Size должен быть > 0)
            if (canvas.Bounds.Width <= 0 || canvas.Bounds.Height <= 0)
            {
                // Можно вручную задать размер, если нужно
                canvas.Measure(new Size(800, 600));
                canvas.Arrange(new Rect(new Size(800, 600)));
            }

            // Создаём RenderTargetBitmap нужного размера
            var pixelSize = new PixelSize((int)canvas.Bounds.Width, (int)canvas.Bounds.Height);
            var dpi = new Vector(96, 96); // стандартный DPI
            var bitmap = new RenderTargetBitmap(pixelSize, dpi);

            // Рендерим элемент в битмап
            bitmap.Render(canvas);



            return RenderTargetBitmapToByteArray(bitmap);
        }


        private static void ProcessCanvas(Canvas canvas, List<ZplElementBase> zplElements)
        {
            var data = CanvasToBytes(canvas);
            try
            {
                char format = 'R'; // Raster format
                zplElements.Add(new ZplDownloadGraphics(format, "main_canvas", data));
                zplElements.Add(new ZplRecallGraphic(0, 0, format, "main_canvas"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        public static byte[] RenderTargetBitmapToByteArray(RenderTargetBitmap bitmap)
        {
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream);
                stream.Position = 0; // Важно: сбросить позицию потока
                return stream.ToArray();
            }
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
            Debug.WriteLine("Сгенерированный ZPL:\n" + zpl);
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