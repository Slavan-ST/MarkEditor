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
using System.Linq;
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
            // 1. Рассчитываем масштаб
            var scale = Dpi / SourceDpi;

            // 2. Извлекаем все ElementViewModel из дочерних контролов канваса
            var elements = new List<ElementViewModel>();

            foreach (var child in canvas.Children)
            {
                if (child is Control control && control.DataContext is ElementViewModel vm)
                {
                    elements.Add(vm);
                }
            }

            // 3. Асинхронно применяем масштабирование
            // ВАЖНО: Это блокирующий вызов. Убедись, что не вызывается в основном UI-потоке, или используй ConfigureAwait(false)
            try
            {
                ApplyScaleAsync(elements, scale).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при масштабировании элементов: {ex.InnerException?.Message ?? ex.Message}");
            }

            // 4. Теперь рендерим уже масштабированный Canvas
            var widthInches = canvas.Width / SourceDpi;
            var heightInches = canvas.Height / SourceDpi;

            var pixelWidth = (int)Math.Ceiling(widthInches * Dpi);
            var pixelHeight = (int)Math.Ceiling(heightInches * Dpi);

            var pixelSize = new PixelSize(pixelWidth, pixelHeight);
            var dpi = new Vector(Dpi, Dpi);

            using var bitmap = new RenderTargetBitmap(pixelSize, dpi);

            using (var context = bitmap.CreateDrawingContext())
            {
                // Теперь контекст рисует уже масштабированные элементы
                var brush = new VisualBrush(canvas)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };

                context.DrawRectangle(brush, null, new Rect(0, 0, canvas.Width, canvas.Height));
            }

            var data = RenderTargetBitmapToByteArray(bitmap);

            try
            {
                zplElements.Add(new ZplDownloadGraphics('R', "label", data));
                zplElements.Add(new ZplRecallGraphic(0, 0, 'R', "label"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при добавлении ZPL-графики: {ex.Message}");
            }
        }

        public static async Task<IEnumerable<ElementViewModel>> ApplyScaleAsync(
    IEnumerable<ElementViewModel> elements,
    double scale)
        {
            var tasks = elements.Select(async element =>
            {
                if (element.Type == ElementType.Image && element.OriginalImageData == null && !string.IsNullOrEmpty(element.Path))
                {
                    try
                    {
                        var bitmap = await Task.Run(() => new Bitmap(element.Path)).ConfigureAwait(false);
                        element.OriginalImageData = bitmap;
                        element.OriginalWidth = bitmap.PixelSize.Width;
                        element.OriginalHeight = bitmap.PixelSize.Height;

                        if (element.Width == 0 || element.Height == 0)
                        {
                            element.Width = bitmap.PixelSize.Width;
                            element.Height = bitmap.PixelSize.Height;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load image: {element.Path}, Error: {ex.Message}");
                    }
                }

                element.X *= scale;
                element.Y *= scale;
                element.Width *= scale;
                element.Height *= scale;

                if (element.OriginalImageData != null && element.OriginalWidth > 0 && element.OriginalHeight > 0)
                {
                    element.ScaleX = element.Width / element.OriginalWidth;
                    element.ScaleY = element.Height / element.OriginalHeight;
                }

                // Обновляем UI — это в UI-потоке, но мы уже в нём, если вызвали синхронно
                Canvas.SetLeft(element.Control, element.X);
                Canvas.SetTop(element.Control, element.Y);
                element.Control.Width = element.Width;
                element.Control.Height = element.Height;

                return element;
            });

            return await Task.WhenAll(tasks).ConfigureAwait(false);
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