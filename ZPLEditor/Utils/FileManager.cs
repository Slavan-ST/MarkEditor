using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ZPLEditor.Utils
{
    public static class FileManager
    {
        /// <summary>
        /// Открывает диалог выбора одного файла с указанными фильтрами.
        /// </summary>
        /// <param name="window">Родительское окно</param>
        /// <param name="filters">Фильтры файлов</param>
        /// <param name="title">Заголовок диалога</param>
        /// <returns>Путь к выбранному файлу или null, если выбор отменён</returns>
        private static async Task<string?> OpenFileAsync(Window window, IEnumerable<FileDialogFilter> filters, string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filters = new List<FileDialogFilter>(filters),
                AllowMultiple = false
            };

            var result = await dialog.ShowAsync(window);
            return result?.Length > 0 ? result[0] : null;
        }

        /// <summary>
        /// Открывает диалог сохранения файла с указанными фильтрами.
        /// </summary>
        /// <param name="window">Родительское окно</param>
        /// <param name="filters">Фильтры файлов</param>
        /// <param name="title">Заголовок диалога</param>
        /// <param name="defaultExtension">Расширение по умолчанию</param>
        /// <returns>Путь к выбранному файлу для сохранения или null, если диалог отменён</returns>
        private static async Task<string?> SaveFileAsync(Window window, IEnumerable<FileDialogFilter> filters, string title, string? defaultExtension = null)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filters = new List<FileDialogFilter>(filters),
                DefaultExtension = defaultExtension
            };

            var result = await dialog.ShowAsync(window);
            return result; // Может быть null, если пользователь отменил диалог
        }

        /// <summary>
        /// Открывает диалог выбора изображения.
        /// </summary>
        /// <param name="window">Родительское окно</param>
        /// <returns>Путь к выбранному изображению или null</returns>
        public static async Task<string?> OpenImageFileAsync(Window window)
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = "Изображения", Extensions = { "png", "jpg", "jpeg", "bmp", "gif" } },
                new FileDialogFilter { Name = "Все файлы", Extensions = { "*" } }
            };

            return await OpenFileAsync(window, filters, "Выберите изображение");
        }

        /// <summary>
        /// Открывает диалог выбора JSON-файла.
        /// </summary>
        /// <param name="window">Родительское окно</param>
        /// <returns>Путь к выбранному JSON-файлу или null</returns>
        public static async Task<string?> OpenJsonFileAsync(Window window)
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = "JSON-файлы", Extensions = { "json" } },
                new FileDialogFilter { Name = "Все файлы", Extensions = { "*" } }
            };

            return await OpenFileAsync(window, filters, "Выберите проект");
        }

        /// <summary>
        /// Открывает диалог сохранения изображения.
        /// </summary>
        /// <param name="window">Родительское окно</param>
        /// <returns>Путь для сохранения изображения или null, если отменено</returns>
        public static async Task<string?> SaveImageFileAsync(Window window)
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = "PNG изображение", Extensions = { "png" } },
                new FileDialogFilter { Name = "JPEG изображение", Extensions = { "jpg", "jpeg" } },
                new FileDialogFilter { Name = "BMP изображение", Extensions = { "bmp" } },
                new FileDialogFilter { Name = "GIF изображение", Extensions = { "gif" } },
                new FileDialogFilter { Name = "Все файлы", Extensions = { "*" } }
            };

            return await SaveFileAsync(window, filters, "Сохранить изображение", "png");
        }

        /// <summary>
        /// Открывает диалог сохранения JSON-файла.
        /// </summary>
        /// <param name="window">Родительское окно</param>
        /// <returns>Путь для сохранения JSON-файла или null, если отменено</returns>
        public static async Task<string?> SaveJsonFileAsync(Window window)
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = "JSON-файлы", Extensions = { "json" } },
                new FileDialogFilter { Name = "Все файлы", Extensions = { "*" } }
            };

            return await SaveFileAsync(window, filters, "Сохранить проект", "json");
        }
    }
}