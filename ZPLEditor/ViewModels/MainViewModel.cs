using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using BinaryKits.Zpl;
using BinaryKits.Zpl.Label;
using BinaryKits.Zpl.Label.Elements;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
using System.Xml.Linq;
using ZPLEditor.Utils;
using ZPLEditor.Views;

namespace ZPLEditor.ViewModels;

/// <summary>
/// Основная ViewModel для редактора ZPL-этикеток.
/// Управляет элементами на холсте, генерацией ZPL и взаимодействием с пользователем.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly MainView _mainWindow;

    // --- Данные и состояние ---

    [Reactive] public ObservableCollection<ElementViewModel> Elements { get; set; } = new();

    /// <summary>
    /// Текущий элемент, который перетаскивается.
    /// </summary>
    private Control _draggedElement;

    /// <summary>
    /// Начальная точка перетаскивания.
    /// </summary>
    private Point _startPoint;

    // --- Свойства представления ---

    /// <summary>
    /// Сгенерированный ZPL-код для отображения.
    /// </summary>
    [Reactive] public string ZplOutput { get; set; } = "";

    // --- Команды ---

    /// <summary>
    /// Команда добавления текстового элемента.
    /// </summary>
    public ReactiveCommand<Unit, Unit> AddTextCommand { get; }

    /// <summary>
    /// Команда добавления изображения.
    /// </summary>
    public ReactiveCommand<Unit, Unit> AddImageCommand { get; }

    /// <summary>
    /// Команда генерации ZPL-кода.
    /// </summary>
    public ReactiveCommand<Unit, Unit> GenerateZplCommand { get; }
    public ReactiveCommand<ElementViewModel, Unit> RemoveElementCommand { get; }

    private void RemoveElement(ElementViewModel element)
    {
        _mainWindow.LabelCanvas.Children.Remove(element.Control);
        Elements.Remove(element);
    }
    // --- Конструктор ---

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="mainWindow">Ссылка на основное окно для доступа к элементам UI.</param>
    public MainViewModel(MainView mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        // Инициализация команд
        AddTextCommand = ReactiveCommand.Create(AddTextBox);
        AddImageCommand = ReactiveCommand.Create(AddImage);
        GenerateZplCommand = ReactiveCommand.Create(() =>
        {
            string zpl = ZPLUtils.GenerateZplFromControls(Elements);
            // ZPLUtils.PrintZPL(zpl); // Раскомментировать для печати
        });
        RemoveElementCommand = ReactiveCommand.Create<ElementViewModel>(RemoveElement);

        // Подписка на события холста
        _mainWindow.LabelCanvas.AddHandler(
            InputElement.PointerPressedEvent,
            Canvas_PointerPressed,
            RoutingStrategies.Tunnel);

        _mainWindow.LabelCanvas.PointerMoved += HandlePointerMoved;
        _mainWindow.LabelCanvas.PointerReleased += HandlePointerReleased;
    }

    // --- Работа с элементами на холсте ---

    #region Добавление элементов

    /// <summary>
    /// Добавляет текстовое поле на холст.
    /// </summary>
    private void AddTextBox()
    {
        var textBox = new TextBox
        {
            Text = "Edit Me",
            FontSize = 16,
            Width = 120,
            Height = 30,
            Background = Brushes.Transparent,
            Focusable = true
        };

        Canvas.SetLeft(textBox, 50);
        Canvas.SetTop(textBox, 50);

        textBox.DoubleTapped += (s, e) =>
        {
            textBox.Focus();
            e.Handled = true;
        };

        textBox.GotFocus += (s, e) =>
        {
            if (_draggedElement == textBox)
                _draggedElement = null;
        };

        var elementVm = new ElementViewModel(textBox, "Text", 50, 50, 120, 30);
        Elements.Add(elementVm);

        _mainWindow.LabelCanvas.Children.Add(textBox);
    }

    /// <summary>
    /// Асинхронно добавляет изображение на холст через диалог выбора файла.
    /// </summary>
    private async void AddImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите изображение",
            Filters = new List<FileDialogFilter>
        {
            new() { Name = "Изображения", Extensions = { "png", "jpg", "jpeg", "bmp", "gif" } },
            new() { Name = "Все файлы", Extensions = { "*" } }
        },
            AllowMultiple = false
        };

        var window = (Window)_mainWindow.GetVisualRoot();
        var result = await dialog.ShowAsync(window);

        if (result == null || result.Length == 0) return;

        var filePath = result[0];

        try
        {
            var bitmap = new Bitmap(filePath);
            var fileName = Path.GetFileNameWithoutExtension(filePath) ?? "IMG1";

            var imageControl = new Image
            {
                Source = bitmap,
                Width = bitmap.PixelSize.Width,
                Height = bitmap.PixelSize.Height,
                Stretch = Stretch.None,
                IsHitTestVisible = true
            };

            Canvas.SetLeft(imageControl, 50);
            Canvas.SetTop(imageControl, 50);

            var data = File.ReadAllBytes(filePath);
            var elementVm = new ElementViewModel(imageControl, fileName, 50, 50, bitmap.PixelSize.Width, bitmap.PixelSize.Height, data);
            Elements.Add(elementVm);

            _mainWindow.LabelCanvas.Children.Add(imageControl);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
        }
    }

    #endregion

    #region Обработка перетаскивания элементов

    /// <summary>
    /// Обработчик нажатия на холст — определяет, по какому элементу кликнули.
    /// </summary>
    private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(_mainWindow.LabelCanvas);
        var hitControl = _mainWindow.LabelCanvas.InputHitTest(pos) as Control;

        Control actualElement = FindParentControlInCanvas(hitControl);

        if (actualElement == null) return;

        var properties = e.GetCurrentPoint(_mainWindow.LabelCanvas).Properties;

        if (properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 1)
            {
                _draggedElement = actualElement;
                _startPoint = pos;
                e.Pointer.Capture(_mainWindow.LabelCanvas);
                e.Handled = true;
            }
            else if (e.ClickCount == 2)
            {
                if (actualElement is TextBox tb)
                    tb.Focus();
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// Находит ближайший родительский Control, который является прямым дочерним элементом холста.
    /// </summary>
    /// <param name="start">Начальный элемент для поиска.</param>
    /// <returns>Найденный элемент или null.</returns>
    private Control FindParentControlInCanvas(Control start)
    {
        var current = start;
        while (current != null)
        {
            if (_mainWindow.LabelCanvas.Children.Contains(current))
                return current;
            current = current.Parent as Control;
        }
        return null;
    }

    /// <summary>
    /// Обработка движения мыши при перетаскивании.
    /// </summary>
    private void HandlePointerMoved(object sender, PointerEventArgs e)
    {
        if (_draggedElement == null) return;

        var pos = e.GetPosition(_mainWindow.LabelCanvas);
        var delta = pos - _startPoint;

        var left = Canvas.GetLeft(_draggedElement) + delta.X;
        var top = Canvas.GetTop(_draggedElement) + delta.Y;

        Canvas.SetLeft(_draggedElement, left);
        Canvas.SetTop(_draggedElement, top);

        // Найдём соответствующий ElementViewModel и обновим его
        var vm = Elements.FirstOrDefault(x => x.Control == _draggedElement);
        if (vm != null)
        {
            vm.X = left;
            vm.Y = top;
        }

        _startPoint = pos;
    }

    /// <summary>
    /// Завершение перетаскивания — освобождение захвата указателя.
    /// </summary>
    private void HandlePointerReleased(object sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        _draggedElement = null;
    }

    #endregion
}