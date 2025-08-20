using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ZPLEditor.Models.DTO;
using ZPLEditor.Utils.Converters;
using ZPLEditor.ViewModels;

namespace ZPLEditor.Utils
{
    public static class ProjectSerializer
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            Converters = { new ByteArrayConverter() }
        };

        public static void SaveProjectInFile(string filePath, MainViewModel viewModel)
        {
            var projectDto = new ProjectDto
            {
                LabelName = viewModel.LabelName,
                LabelWidth = viewModel.LabelWidth,
                LabelHeight = viewModel.LabelHeight,
                Elements = viewModel.Elements.Select(e => new ElementDataDto(e)).ToList()
            };

            var json = JsonSerializer.Serialize(projectDto, _options);
            File.WriteAllText(filePath, json);
        }

        public static ProjectDto LoadProject(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ProjectDto>(json, _options)
                   ?? new ProjectDto(); // возвращаем пустой проект, если ошибка
        }

        public static void LoadProjectFromFile(string filePath, MainViewModel viewModel)
        {
            var projectDto = LoadProject(filePath);

            // Очищаем текущий холст
            viewModel.Elements.Clear();
            viewModel._mainWindow.LabelCanvas.Children.Clear();

            // Восстанавливаем свойства
            viewModel.LabelName = projectDto.LabelName;
            viewModel.LabelWidth = projectDto.LabelWidth;
            viewModel.LabelHeight = projectDto.LabelHeight;

            // Восстанавливаем элементы
            foreach (var dto in projectDto.Elements)
            {
                Control control = CreateControlForType(dto.Type);

                // Только для текста — делаем TextBox, иначе TextBlock
                if (dto.Type == ElementType.Text)
                {
                    var newTextBox = new TextBox
                    {
                        Text = dto.Content,
                        FontSize = 16,
                        Width = dto.Width,
                        Height = dto.Height,
                        Background = Brushes.Transparent,
                        Focusable = true
                    };
                    control = newTextBox;
                }

                var vm = dto.ToViewModel(control);
                viewModel.Elements.Add(vm);

                // Для текста — добавляем логику редактирования (аналогично AddTextBox)
                if (dto.Type == ElementType.Text && control is TextBox textBox)
                {
                    SetupTextBoxEditing(textBox, vm, viewModel);
                }

                // Для изображений и баркодов — устанавливаем Source, если есть данные
                if (control is Image image)
                {
                    if (dto.Data != null && dto.Type == ElementType.Image)
                    {
                        using var ms = new MemoryStream(dto.Data);
                        image.Source = new Bitmap(ms);
                    }
                    else if (dto.Type != ElementType.Image)
                    {
                        // Перегенерируем баркод
                        try
                        {
                            var bitmap = dto.Type switch
                            {
                                ElementType.QrCode => BarcodeGenerator.GenerateQrCode(dto.Content, (int)dto.Width, (int)dto.Height),
                                ElementType.Ean13 => BarcodeGenerator.GenerateEan13(dto.Content, (int)dto.Width, (int)dto.Height),
                                ElementType.DataMatrix => BarcodeGenerator.GenerateDataMatrix(dto.Content, (int)dto.Width, (int)dto.Height),
                                ElementType.Ean128 => BarcodeGenerator.GenerateCode128(dto.Content, (int)dto.Width, (int)dto.Height),
                                _ => null
                            };
                            if (bitmap != null)
                            {
                                image.Source = bitmap;
                                image.Width = bitmap.PixelSize.Width;
                                image.Height = bitmap.PixelSize.Height;
                            }
                        }
                        catch { /* ignore */ }
                    }
                }

                viewModel._mainWindow.LabelCanvas.Children.Add(control);
            }
        }

        private static void SetupTextBoxEditing(TextBox textBox, ElementViewModel vm, MainViewModel mainVm)
        {
            textBox.DoubleTapped += (s, e) => textBox.Focus();

            textBox.GotFocus += (s, e) => vm.IsEditing = true;

            textBox.LostFocus += (s, e) =>
            {
                if (vm.IsEditing)
                {
                    vm.IsEditing = false;
                    vm.Content = textBox.Text;
                }
            };

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    textBox.Focusable = false;
                    textBox.Focusable = true;
                    vm.IsEditing = false;
                }
            };

            vm.WhenAnyValue(x => x.Content)
                .Where(content => !vm.IsEditing)
                .Subscribe(content =>
                {
                    if (textBox.Text != content)
                        textBox.Text = content;
                });
        }
        public static Control CreateControlForType(ElementType type)
        {
            return type switch
            {
                ElementType.Text => new TextBlock(),
                ElementType.Image => new Image(),
                ElementType.QrCode => new Image(),
                ElementType.Ean13 => new Image(),
                ElementType.Ean128 => new Image(),
                ElementType.DataMatrix => new Image(),
                _ => new Border() // fallback
            };
        }
    }
}
