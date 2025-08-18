using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using ZPLEditor.Models;
using ZPLEditor.Views;

namespace ZPLEditor.Utils
{
    public static class ZPLUtils
    {
        /// <summary>
        /// Генерирует ZPL-код на основе элементов на холсте и отправляет на принтер.
        /// </summary>
        public static string GenerateZplFromControls(IEnumerable<LabelElement> controls)
        {
            var zplElements = new List<ZplElementBase>();

            foreach (var element in controls)
            {
                if (element.Control is TextBox tb)
                {
                    var x = (int)Canvas.GetLeft(tb);
                    var y = (int)Canvas.GetTop(tb);
                    var font = new ZplFont(fontWidth: 50, fontHeight: 50);
                    zplElements.Add(new ZplTextField(tb.Text, x, y, font));
                }
                else if (element.Control is TextBlock tl)
                {
                    var x = (int)Canvas.GetLeft(tl);
                    var y = (int)Canvas.GetTop(tl);
                    var font = new ZplFont(fontWidth: 50, fontHeight: 50);
                    zplElements.Add(new ZplTextField(tl.Text, x, y, font));
                }
                else if (element.Control is Image img)
                {
                    var x = (int)Canvas.GetLeft(img);
                    var y = (int)Canvas.GetTop(img);

                    try
                    {
                        zplElements.Add(new ZplDownloadGraphics('R', element.Name, element.Data));
                        zplElements.Add(new ZplRecallGraphic(x, y, 'R', element.Name));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка конвертации изображения: {ex.Message}");
                    }
                }
            }

            var engine = new ZplEngine(zplElements);
            var options = new ZplRenderOptions { AddEmptyLineBeforeElementStart = true, TargetPrintDpi = 304, SourcePrintDpi = 304 };
            var zpl = engine.ToZplString(options);
            Debug.WriteLine(zpl);
            return zpl;

        }


        /// <summary>
        /// Отправляет ZPL-код на принтер по TCP.
        /// </summary>
        public static void PrintZPL(string zpl)
        {
            const string printerIp = "192.168.10.202";
            const int printerPort = 9100;

            try
            {
                using var client = new TcpClient(printerIp, printerPort);
                using var stream = client.GetStream();
                using var writer = new StreamWriter(stream) { AutoFlush = true };
                writer.Write(zpl);
                Debug.WriteLine("ZPL отправлен на принтер.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка печати: {ex.Message}");
            }
        }
    }
}
