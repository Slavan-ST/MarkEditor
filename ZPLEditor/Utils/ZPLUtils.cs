using Avalonia.Controls;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ZPLEditor.Models;

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

        /// <summary>
        /// Генерирует ZPL-код на основе списка элементов метки.
        /// Поддерживает TextBox, TextBlock и Image.
        /// </summary>
        /// <param name="controls">Список элементов метки (с привязкой к UI-контролам)</param>
        /// <returns>Сгенерированный ZPL-код как строка</returns>
        public static string GenerateZplFromControls(IEnumerable<LabelElement> controls)
        {
            if (controls == null)
                throw new ArgumentNullException(nameof(controls));

            var zplElements = new List<ZplElementBase>();

            foreach (var element in controls)
            {
                try
                {
                    if (element.Control is TextBox textBox)
                    {
                        ProcessTextBox(textBox, element, zplElements);
                    }
                    else if (element.Control is TextBlock textBlock)
                    {
                        ProcessTextBlock(textBlock, element, zplElements);
                    }
                    else if (element.Control is Image image)
                    {
                        ProcessImage(image, element, zplElements);
                    }
                    else
                    {
                        Debug.WriteLine($"Неподдерживаемый тип элемента: {element.Control?.GetType().Name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при обработке элемента '{element.Name}': {ex.Message}");
                }
            }

            return BuildZplString(zplElements);
        }

        private static void ProcessTextBox(TextBox textBox, LabelElement element, List<ZplElementBase> zplElements)
        {
            AddTextElement(textBox.Text, element, zplElements);
        }

        private static void ProcessTextBlock(TextBlock textBlock, LabelElement element, List<ZplElementBase> zplElements)
        {
            AddTextElement(textBlock.Text, element, zplElements);
        }

        private static void AddTextElement(string text, LabelElement element, List<ZplElementBase> zplElements)
        {
            var x = (int)Canvas.GetLeft(element.Control);
            var y = (int)Canvas.GetTop(element.Control);
            var font = new ZplFont(fontWidth: 50, fontHeight: 50); // Можно сделать настраиваемым

            zplElements.Add(new ZplTextField(text, x, y, font));
        }

        private static void ProcessImage(Image image, LabelElement element, List<ZplElementBase> zplElements)
        {
            var x = (int)Canvas.GetLeft(image);
            var y = (int)Canvas.GetTop(image);

            if (string.IsNullOrEmpty(element.Name) || element.Data == null || element.Data.Length == 0)
            {
                Debug.WriteLine("Изображение не содержит данных или имени для загрузки.");
                return;
            }

            try
            {
                // Загружаем изображение в память принтера под именем (например, 'R:<name>')
                char format = 'R'; // Raster format
                zplElements.Add(new ZplDownloadGraphics(format, element.Name, element.Data));
                zplElements.Add(new ZplRecallGraphic(x, y, format, element.Name));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при добавлении изображения '{element.Name}': {ex.Message}");
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