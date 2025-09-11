using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarkEditor.Utils
{
    public static class CanvasExporter
    {

        /// <summary>
        /// /Сохранение canvas в файл - тестовый метод
        /// </summary>
        /// <param name="canvas"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static void SaveCanvasToFile(Canvas canvas, string filePath)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            // Получаем размеры Canvas
            var width = (int)canvas.Bounds.Width;
            var height = (int)canvas.Bounds.Height;

            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("Canvas has invalid size.");
            }

            // Создаём RenderTargetBitmap нужного размера
            using var renderTarget = new RenderTargetBitmap(
                new PixelSize(width, height),
                new Vector(96, 96) // DPI
            );

            // Рендерим Canvas в битмап
            canvas.Measure(new Size(width, height));
            canvas.Arrange(new Rect(0, 0, width, height));
            renderTarget.Render(canvas);

            // Сохраняем как PNG
            using var stream = File.OpenWrite(filePath);
            renderTarget.Save(stream);
        }
    }
}
