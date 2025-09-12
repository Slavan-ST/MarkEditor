using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarkEditor.Utils
{
    public static class FileManager
    {
        /// <summary>
        /// Открывает диалог выбора одного файла с указанными фильтрами.
        /// </summary>
        private static async Task<string?> OpenFileAsync(Window window, FilePickerFileType[] filters, string title)
        {
            var topLevel = TopLevel.GetTopLevel(window);
            if (topLevel?.StorageProvider is not { } storageProvider)
                return null;

            var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = filters
            });

            return result.FirstOrDefault()?.Path?.LocalPath;
        }

        /// <summary>
        /// Открывает диалог сохранения файла с указанными фильтрами.
        /// </summary>
        private static async Task<string?> SaveFileAsync(Window window, FilePickerFileType[] filters, string title, string? defaultExtension = null)
        {
            var topLevel = TopLevel.GetTopLevel(window);
            if (topLevel?.StorageProvider is not { } storageProvider)
                return null;

            var options = new FilePickerSaveOptions
            {
                Title = title,
                FileTypeChoices = filters,
                DefaultExtension = defaultExtension,
                SuggestedFileName = "untitled" // можно настроить по необходимости
            };

            var result = await storageProvider.SaveFilePickerAsync(options);
            return result?.Path?.LocalPath;
        }

        /// <summary>
        /// Открывает диалог выбора изображения.
        /// </summary>
        public static async Task<string?> OpenImageFileAsync(Window window)
        {
            var imageFilter = new FilePickerFileType("Изображения")
            {
                Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif"],
                AppleUniformTypeIdentifiers = ["public.image"],
                MimeTypes = ["image/*"]
            };

            var allFilter = new FilePickerFileType("Все файлы")
            {
                Patterns = ["*"]
            };

            return await OpenFileAsync(window, [imageFilter, allFilter], "Выберите изображение");
        }

        /// <summary>
        /// Открывает диалог выбора JSON-файла.
        /// </summary>
        public static async Task<string?> OpenJsonFileAsync(Window window)
        {
            var jsonFilter = new FilePickerFileType("JSON-файлы")
            {
                Patterns = ["*.json"],
                MimeTypes = ["application/json"]
            };

            var allFilter = new FilePickerFileType("Все файлы")
            {
                Patterns = ["*"]
            };

            return await OpenFileAsync(window, [jsonFilter, allFilter], "Выберите проект");
        }

        /// <summary>
        /// Открывает диалог сохранения изображения.
        /// </summary>
        public static async Task<string?> SaveImageFileAsync(Window window)
        {
            var pngFilter = new FilePickerFileType("PNG изображение")
            {
                Patterns = ["*.png"],
                MimeTypes = ["image/png"]
            };

            var jpegFilter = new FilePickerFileType("JPEG изображение")
            {
                Patterns = ["*.jpg", "*.jpeg"],
                MimeTypes = ["image/jpeg"]
            };

            var bmpFilter = new FilePickerFileType("BMP изображение")
            {
                Patterns = ["*.bmp"],
                MimeTypes = ["image/bmp"]
            };

            var gifFilter = new FilePickerFileType("GIF изображение")
            {
                Patterns = ["*.gif"],
                MimeTypes = ["image/gif"]
            };

            var allFilter = new FilePickerFileType("Все файлы")
            {
                Patterns = ["*"]
            };

            return await SaveFileAsync(window, [pngFilter, jpegFilter, bmpFilter, gifFilter, allFilter], "Сохранить изображение", "png");
        }

        /// <summary>
        /// Открывает диалог сохранения JSON-файла.
        /// </summary>
        public static async Task<string?> SaveJsonFileAsync(Window window)
        {
            var jsonFilter = new FilePickerFileType("JSON-файлы")
            {
                Patterns = ["*.json"],
                MimeTypes = ["application/json"]
            };

            var allFilter = new FilePickerFileType("Все файлы")
            {
                Patterns = ["*"]
            };

            return await SaveFileAsync(window, [jsonFilter, allFilter], "Сохранить проект", "json");
        }
    }
}