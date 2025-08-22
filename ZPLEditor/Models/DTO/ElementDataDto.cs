using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System.IO;
using ZPLEditor.ViewModels;

namespace ZPLEditor.Models.DTO
{
    /// <summary>
    /// DTO (Data Transfer Object) для сериализации и передачи данных элемента.
    /// Используется для сохранения состояния элемента и его восстановления.
    /// </summary>
    public class ElementDataDto
    {
        // === Основные свойства элемента ===
        public string Name { get; set; } = string.Empty;
        public ElementType Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        // === Данные элемента (изображение или текст) ===
        public byte[]? Data { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        // === Свойства, связанные с масштабированием изображений ===
        public double OriginalWidth { get; set; }
        public double OriginalHeight { get; set; }
        public double ScaleX { get; set; } = 1.0;
        public double ScaleY { get; set; } = 1.0;

        // === Свойства текста и вращения ===
        public double FontSize { get; set; } = 12.0;
        public double Rotation { get; set; } = 0.0;

        // === Конструкторы ===

        /// <summary>
        /// Конструктор по умолчанию (необходим для десериализации).
        /// </summary>
        public ElementDataDto() { }

        /// <summary>
        /// Создаёт DTO на основе ViewModel (для сохранения состояния).
        /// </summary>
        /// <param name="vm">Исходная ViewModel элемента.</param>
        public ElementDataDto(ElementViewModel vm)
        {
            Name = vm.Name;
            Type = vm.Type;
            X = vm.X;
            Y = vm.Y;
            Width = vm.Width;
            Height = vm.Height;
            Data = vm.Data;
            Content = vm.Content;
            Path = vm.Path;

            OriginalWidth = vm.OriginalWidth;
            OriginalHeight = vm.OriginalHeight;
            ScaleX = vm.ScaleX;
            ScaleY = vm.ScaleY;
            FontSize = vm.FontSize;
            Rotation = vm.Rotation;
        }

        // === Методы ===

        /// <summary>
        /// Преобразует DTO обратно в ViewModel.
        /// </summary>
        /// <param name="control">Родительский элемент управления, к которому привязывается ViewModel.</param>
        /// <returns>Новая ViewModel с восстановленными данными.</returns>
        public ElementViewModel ToViewModel(Control control)
        {
            var viewModel = new ElementViewModel(
                control: control,
                name: Name,
                type: Type,
                x: X,
                y: Y,
                width: Width,
                height: Height,
                data: Data,
                content: Content,
                path: Path,
                originalImageData: Data != null
                    ? new Bitmap(new MemoryStream(Data))
                    : null
            );

            // Устанавливаем дополнительные свойства, не передаваемые через конструктор
            viewModel.OriginalWidth = OriginalWidth;
            viewModel.OriginalHeight = OriginalHeight;
            viewModel.ScaleX = ScaleX;
            viewModel.ScaleY = ScaleY;
            viewModel.FontSize = FontSize;
            viewModel.Rotation = Rotation;

            // Примечание: IsEditing, IsContentVisible и другие производные свойства
            // будут автоматически обновлены через WhenAnyValue(Type) в ViewModel.

            return viewModel;
        }
    }
}