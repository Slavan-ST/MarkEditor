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
using System.Text.Json;
using ZPLEditor.Models.DTO;
using ZPLEditor.Utils.Converters;
using ZPLEditor.ViewModels;

namespace ZPLEditor.Utils
{
    /// <summary>
    /// Утилита для сериализации и десериализации проекта редактора этикеток.
    /// Поддерживает сохранение/загрузку в формате JSON с кастомной обработкой бинарных данных.
    /// </summary>
    public static class ProjectSerializer
    {
        // === Настройки сериализации JSON ===
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            Converters = { new ByteArrayConverter() }
        };

        // === Публичные методы: Save / Load ===

        /// <summary>
        /// Сохраняет текущий проект в файл.
        /// </summary>
        /// <param name="filePath">Путь к файлу для сохранения.</param>
        /// <param name="viewModel">MainViewModel, содержащий данные проекта.</param>
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

        /// <summary>
        /// Загружает проект из файла без применения к UI.
        /// </summary>
        /// <param name="filePath">Путь к файлу проекта.</param>
        /// <returns>DTO проекта или пустой объект при ошибке.</returns>
        public static ProjectDto LoadProject(string filePath)
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<ProjectDto>(json, _options)
                   ?? new ProjectDto();
        }

        /// <summary>
        /// Полностью загружает проект из файла и восстанавливает его в UI.
        /// </summary>
        /// <param name="filePath">Путь к файлу проекта.</param>
        /// <param name="viewModel">MainViewModel, в который загружается проект.</param>
        public static void LoadProjectFromFile(string filePath, MainViewModel viewModel)
        {
            var projectDto = LoadProject(filePath);

            // Очистка текущего состояния
            ClearCanvas(viewModel);

            // Восстановление свойств проекта
            RestoreProjectProperties(viewModel, projectDto);

            // Восстановление элементов
            RestoreElements(viewModel, projectDto.Elements);
        }

        // === Внутренние методы: Очистка и восстановление ===

        /// <summary>
        /// Очищает холст и коллекцию элементов.
        /// </summary>
        private static void ClearCanvas(MainViewModel viewModel)
        {
            viewModel.Elements.Clear();
            viewModel._mainWindow?.LabelCanvas?.Children.Clear();
        }

        /// <summary>
        /// Восстанавливает основные свойства проекта (имя, размеры).
        /// </summary>
        private static void RestoreProjectProperties(MainViewModel viewModel, ProjectDto projectDto)
        {
            viewModel.LabelName = projectDto.LabelName;
            viewModel.LabelWidth = projectDto.LabelWidth;
            viewModel.LabelHeight = projectDto.LabelHeight;
        }

        /// <summary>
        /// Восстанавливает все элементы из DTO в UI.
        /// </summary>
        private static void RestoreElements(MainViewModel viewModel, List<ElementDataDto> elementDtos)
        {
            foreach (var dto in elementDtos)
            {
                var control = CreateControlForType(dto.Type);

                if (dto.Type == ElementType.Text)
                {
                    var textBox = CreateEditableTextBox(dto);
                    control = textBox;
                    var vm = dto.ToViewModel(textBox);
                    SetupTextBoxEditing(textBox, vm, viewModel);
                    viewModel.Elements.Add(vm);
                }
                else
                {
                    var vm = dto.ToViewModel(control);
                    viewModel.Elements.Add(vm);

                    if (control is Image image)
                    {
                        ApplyImageOrBarcode(image, dto);
                    }
                }

                viewModel._mainWindow?.LabelCanvas?.Children.Add(control);
            }
        }

        /// <summary>
        /// Настраивает редактирование TextBox (фокус, события, привязка контента).
        /// </summary>
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
                if (e.Key is Key.Enter or Key.Escape)
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

        // === Создание элементов UI ===

        /// <summary>
        /// Создаёт базовый UI-элемент в зависимости от типа элемента.
        /// </summary>
        /// <param name="type">Тип элемента.</param>
        /// <returns>Новый Control.</returns>
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

        /// <summary>
        /// Создаёт редактируемый TextBox на основе DTO.
        /// </summary>
        private static TextBox CreateEditableTextBox(ElementDataDto dto)
        {
            return new TextBox
            {
                Text = dto.Content,
                FontSize = 16,
                Width = dto.Width,
                Height = dto.Height,
                Background = Brushes.Transparent,
                Focusable = true
            };
        }

        /// <summary>
        /// Устанавливает изображение или генерирует баркод в зависимости от типа.
        /// </summary>
        private static void ApplyImageOrBarcode(Image image, ElementDataDto dto)
        {
            if (dto.Data != null && dto.Type == ElementType.Image)
            {
                using var ms = new MemoryStream(dto.Data);
                image.Source = new Bitmap(ms);
            }
            else
            {
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
                catch
                {
                    // Игнорируем ошибки генерации баркода
                }
            }
        }
    }
}