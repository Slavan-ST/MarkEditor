using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZPLEditor.ViewModels;

namespace ZPLEditor.Models.DTO
{
    public class ElementDataDto
    {
        public string Name { get; set; } = string.Empty;
        public ElementType Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public byte[]? Data { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        // Конструкторы
        public ElementDataDto() { }

        // Конструктор из ViewModel (для сохранения)
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
        }

        // Метод для восстановления ViewModel из DTO
        public ElementViewModel ToViewModel(Control control)
        {
            return new ElementViewModel(
                control: control,
                name: Name,
                type: Type,
                x: X,
                y: Y,
                width: Width,
                height: Height,
                data: Data,
                content: Content,
                path: Path
            );
        }
    }
}
