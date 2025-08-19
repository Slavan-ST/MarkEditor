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
using System.Reactive.Linq;
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
    [Reactive] public List<ElementViewModel> Elements { get; set; } = new();
    [Reactive] public ElementViewModel? CurrentElement { get; set; } = null;
    [Reactive] public bool IsCurrentElement { get; set; } = false;
    [Reactive] public double LabelWidth { get; set; } = 100;
    [Reactive] public double LabelHeight { get; set; } = 100;
    [Reactive] public string LabelName { get; set; } = string.Empty;

    /// <summary>
    /// Текущий элемент, который перетаскивается.
    /// </summary>
    private Control? _draggedElement;

    /// <summary>
    /// Начальная точка перетаскивания.
    /// </summary>
    private Point _startPoint;


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

    /// <summary>
    /// Команда печати ZPL.
    /// </summary>
    public ReactiveCommand<Unit, Unit> PrintZplCommand { get; }

    /// <summary>
    /// Команда удаления элемента.
    /// </summary>
    public ReactiveCommand<ElementViewModel, Unit> RemoveElementCommand { get; }
    public ReactiveCommand<Unit, Unit> AddQrCodeCommand { get; }
    public ReactiveCommand<Unit, Unit> AddEan13Command { get; }
    public ReactiveCommand<Unit, Unit> AddDataMatrixCommand { get; }
    public ReactiveCommand<Unit, Unit> AddCode128Command { get; }


    // --- Конструктор ---
    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="mainWindow">Ссылка на основное окно для доступа к элементам UI.</param>
    public MainViewModel(MainView mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        // Инициализация команд
        // В конструкторе MainViewModel после других команд
        AddQrCodeCommand = ReactiveCommand.Create(AddQrCode);
        AddEan13Command = ReactiveCommand.Create(AddEan13);
        AddDataMatrixCommand = ReactiveCommand.Create(AddDataMatrix);
        AddCode128Command = ReactiveCommand.Create(AddCode128);

        AddTextCommand = ReactiveCommand.Create(AddTextBox);
        AddImageCommand = ReactiveCommand.Create(AddImage);

        PrintZplCommand = ReactiveCommand.Create(() =>
        {
            string zpl = ZPLUtils.GenerateZplFromCanvas(_mainWindow.LabelCanvas);
            ZPLUtils.PrintZPL(zpl);
        });
        GenerateZplCommand = ReactiveCommand.Create(() =>
        {
            string zpl = ZPLUtils.GenerateZplFromCanvas(_mainWindow.LabelCanvas);
            Debug.WriteLine(zpl);
        });

        RemoveElementCommand = ReactiveCommand.Create<ElementViewModel>(RemoveElement);

        // Подписка на события холста
        _mainWindow.LabelCanvas.AddHandler(
            InputElement.PointerPressedEvent,
            Canvas_PointerPressed,
            RoutingStrategies.Tunnel);

        this.WhenAnyValue(x => x.CurrentElement)
            .Select(element => element != null)
            .BindTo(this, x => x.IsCurrentElement);

        this.WhenAnyValue(x => x.LabelWidth)
            .Where(w => w > 0)
            .Subscribe(w => LabelWidth = w);

        this.WhenAnyValue(x => x.LabelHeight)
            .Where(h => h > 0)
            .Subscribe(h => LabelHeight = h);

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
        CurrentElement = elementVm;
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
                Stretch = Stretch.Fill,
                IsHitTestVisible = true
            };

            Canvas.SetLeft(imageControl, 50);
            Canvas.SetTop(imageControl, 50);

            var data = File.ReadAllBytes(filePath);
            var elementVm = new ElementViewModel(imageControl, fileName, 50, 50, bitmap.PixelSize.Width, bitmap.PixelSize.Height, data);
            Elements.Add(elementVm);
            elementVm.Path = filePath;

            _mainWindow.LabelCanvas.Children.Add(imageControl);
            CurrentElement = elementVm;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
        }
    }

    private void AddQrCode()
    {
        var content = $"QR-{LabelName}-{DateTime.Now:HHmmss}";
        var bitmap = BarcodeGenerator.GenerateQrCode(content, 100, 100);
        AddBarcodeToCanvas(bitmap, "QR", content);
    }

    private void AddEan13()
    {
        // Генерируем тестовый EAN-13: 12 цифр + контрольная (можно рандомизировать)
        var now = DateTime.Now;
        var baseNum = $"{now.Hour:D2}{now.Minute:D2}{now.Second:D2}123";
        var ean13 = baseNum + CalculateEan13CheckDigit(baseNum); // Делаем 13 цифр
        var bitmap = BarcodeGenerator.GenerateEan13(ean13, 200, 80);
        AddBarcodeToCanvas(bitmap, "EAN13", ean13);
    }

    private void AddDataMatrix()
    {
        var content = $"DM-X:{LabelWidth:F0},Y:{LabelHeight:F0}";
        var bitmap = BarcodeGenerator.GenerateDataMatrix(content, 100, 100);
        AddBarcodeToCanvas(bitmap, "DataMatrix", content);
    }

    private void AddCode128()
    {
        var content = $"[)>{LabelName}|{DateTime.Now:yyMMdd}|{LabelWidth}x{LabelHeight}";
        var bitmap = BarcodeGenerator.GenerateCode128(content, 200, 80);
        AddBarcodeToCanvas(bitmap, "Code128", content);
    }

    // Вспомогательный метод: добавляет баркод как изображение на холст
    private void AddBarcodeToCanvas(Bitmap bitmap, string typeName, string data)
    {
        var imageControl = new Image
        {
            Source = bitmap,
            Width = bitmap.PixelSize.Width,
            Height = bitmap.PixelSize.Height,
            Stretch = Stretch.Fill,
            IsHitTestVisible = true
        };

        double left = 50;
        double top = 50;

        Canvas.SetLeft(imageControl, left);
        Canvas.SetTop(imageControl, top);

        var elementVm = new ElementViewModel(
            imageControl,
            $"{typeName}_{Elements.Count + 1}",
            left, top,
            bitmap.PixelSize.Width,
            bitmap.PixelSize.Height,
            null // можно хранить байты, если нужно
        );
        elementVm.Path = $"Generated:{typeName}='{data}'"; // для отображения в UI

        Elements.Add(elementVm);
        _mainWindow.LabelCanvas.Children.Add(imageControl);
        CurrentElement = elementVm;
    }

    // Вспомогательный метод: вычисление контрольной цифры EAN-13
    private string CalculateEan13CheckDigit(string ean12)
    {
        if (ean12.Length != 12 || !ean12.All(char.IsDigit))
            throw new ArgumentException("EAN-12 must be 12 digits.");

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int digit = ean12[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }
        int checkDigit = (10 - (sum % 10)) % 10;
        return checkDigit.ToString();
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

                // Устанавливаем текущий элемент при клике
                var vm = Elements.FirstOrDefault(x => x.Control == actualElement);
                if (vm != null)
                {
                    CurrentElement = vm;
                }
            }
            else if (e.ClickCount == 2)
            {
                if (actualElement is TextBox tb)
                    tb.Focus();
                e.Handled = true;

                // Двойной клик — тоже делаем элемент текущим
                var vm = Elements.FirstOrDefault(x => x.Control == actualElement);
                if (vm != null)
                {
                    CurrentElement = vm;
                }
            }
        }
    }

    /// <summary>
    /// Находит ближайший родительский Control, который является прямым дочерним элементом холста.
    /// </summary>
    /// <param name="start">Начальный элемент для поиска.</param>
    /// <returns>Найденный элемент или null.</returns>
    private Control? FindParentControlInCanvas(Control start)
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


    #region Удаление элементов

    /// <summary>
    /// Удаляет элемент с холста и из коллекции.
    /// </summary>
    /// <param name="element">Элемент для удаления.</param>
    private void RemoveElement(ElementViewModel element)
    {
        if (_mainWindow.LabelCanvas.Children.Contains(element.Control))
        {
            _mainWindow.LabelCanvas.Children.Remove(element.Control);
            Elements.Remove(element);

            if (CurrentElement == element)
            {
                CurrentElement = null;
            }
        }
    }

    #endregion
}