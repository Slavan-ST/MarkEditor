using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using DynamicData;
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

        public static string GenerateZplFromCanvas(Canvas canvas, List<ElementViewModel> elements)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            var zplElements = new List<ZplElementBase>();

            ProcessCanvas(elements,canvas, zplElements);

            //CanvasExporter.SaveCanvasToFile(canvas, "C:\\temp\\debug.png"); // тест изображения
            return BuildZplString(zplElements);
        }



        private static void ProcessCanvas(List<ElementViewModel> elements, Canvas canvas, List<ZplElementBase> zplElements)
        {
            var oldWidth = canvas.Width;
            var oldHeight = canvas.Height;

            // 1. Рассчитываем масштаб
            var scale = Dpi / SourceDpi;

            // 3. Масштабируем все элементы
            ElementViewModel.ScaleElements(elements, scale);

            // 4. Создаём новый Canvas с масштабированными размерами
            var scaledCanvasWidth = canvas.Width * scale;
            var scaledCanvasHeight = canvas.Height * scale;

            canvas.Height = scaledCanvasHeight;
            canvas.Width = scaledCanvasWidth;


            // 6. Подготавливаем размеры для рендера
            var widthInches = scaledCanvasWidth ;
            var heightInches = scaledCanvasHeight ;

            var pixelWidth = (int)Math.Ceiling(widthInches );
            var pixelHeight = (int)Math.Ceiling(heightInches );

            var pixelSize = new PixelSize(pixelWidth, pixelHeight);
            var dpiVector = new Vector(Dpi, Dpi);

            using var bitmap = new RenderTargetBitmap(pixelSize, dpiVector);

            // 7. Рендерим Canvas в битмап
            bitmap.Render(canvas);

            // 8. Конвертируем в байты
            var data = RenderTargetBitmapToByteArray(bitmap);

            // 9. Добавляем ZPL команды
            try
            {
                zplElements.Add(new ZplDownloadGraphics('R', "label", data));
                zplElements.Add(new ZplRecallGraphic(0, 0, 'R', "label"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при добавлении ZPL-графики: {ex.Message}");
            }

            canvas.Width = oldWidth;
            canvas.Height = oldHeight;
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
                SourcePrintDpi = SourceDpi
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