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
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
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
    public readonly MainView _mainWindow;

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
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadCommand { get; }


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

        SaveCommand = ReactiveCommand.CreateFromTask(Save);
        LoadCommand = ReactiveCommand.CreateFromTask(Load);

        AddTextCommand = ReactiveCommand.Create(AddTextBox);
        AddImageCommand = ReactiveCommand.Create(AddImage);

        PrintZplCommand = ReactiveCommand.Create(() =>
        {
            string zpl = ZPLUtils.GenerateZplFromCanvas(_mainWindow.LabelCanvas, Elements);
            ZPLUtils.PrintZPL(zpl);
        });
        GenerateZplCommand = ReactiveCommand.Create(() =>
        {
            string zpl = ZPLUtils.GenerateZplFromCanvas(_mainWindow.LabelCanvas, Elements);
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
    #region Сохранение/загрузка этикетки
    public async Task Save()
    {
        try
        {
            var window = (Window)_mainWindow.GetVisualRoot();
            var filePath = await FileManager.SaveJsonFileAsync(window);
            if (!string.IsNullOrEmpty(filePath))
            {
                ProjectSerializer.SaveProjectInFile(filePath, this);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка при сохранении: {ex.Message}");
        }
    }

    public async Task Load()
    {
        try
        {
            var window = (Window)_mainWindow.GetVisualRoot();
            var filePath = await FileManager.OpenJsonFileAsync(window);
            if (!string.IsNullOrEmpty(filePath))
            {
                ProjectSerializer.LoadProjectFromFile(filePath, this);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка при загрузке: {ex.Message}");
        }
    }

    #endregion
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

        var elementVm = new ElementViewModel(textBox, "Text", ElementType.Text, 50, 50, 120, 30)
        {
            Content = "Edit Me"
        };
        Elements.Add(elementVm);

        _mainWindow.LabelCanvas.Children.Add(textBox);
        CurrentElement = elementVm;

        // --- Управление редактированием ---
        void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && elementVm.IsEditing)
            {
                elementVm.IsEditing = false;
                elementVm.Content = tb.Text; // Сохраняем только здесь
            }
        }

        void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                textBox.LostFocus -= OnTextBoxLostFocus; // Избегаем дублирования
                textBox.LostFocus += OnTextBoxLostFocus;
                textBox.Focusable = false; // Снимаем фокус
                textBox.Focusable = true;
                elementVm.IsEditing = false;
            }
        }

        textBox.DoubleTapped += (s, e) =>
        {
            textBox.Focus();
            e.Handled = true;
        };

        textBox.GotFocus += (s, e) =>
        {
            elementVm.IsEditing = true;
            if (_draggedElement == textBox)
                _draggedElement = null;
        };

        textBox.LostFocus += OnTextBoxLostFocus;
        textBox.KeyDown += OnTextBoxKeyDown;

        // Реактивное обновление Text из Content (только когда VM меняет Content)
        elementVm.WhenAnyValue(vm => vm.Content)
            .Where(content => !elementVm.IsEditing)
            .Subscribe(content =>
            {
                if (textBox.Text != content)
                    textBox.Text = content;
            });
    }

    /// <summary>
    /// Асинхронно добавляет изображение на холст через диалог выбора файла.
    /// </summary>
    private async void AddImage()
    {
        var filePath = await FileManager.OpenImageFileAsync((Window)_mainWindow.GetVisualRoot());

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
            var elementVm = new ElementViewModel(imageControl, fileName,ElementType.Image, 50, 50, bitmap.PixelSize.Width, bitmap.PixelSize.Height, data);
            Elements.Add(elementVm);
            elementVm.Path = filePath;
            elementVm.Type = ElementType.Image;
            elementVm.OriginalImageData = bitmap;

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
        var bitmap = BarcodeGenerator.GenerateQrCode(content);
        AddBarcodeToCanvas(bitmap, "QR", content, ElementType.QrCode);
    }

    private void AddEan13()
    {
        var now = DateTime.Now;
        // ГГММДДЧЧММСС → берём часть
        var baseNum = $"{now.Year % 100:D2}{now.Month:D2}{now.Day:D2}{now.Hour:D2}{now.Minute:D2}{now.Second:D2}";
        // Пример: 250315143045 → 12 цифр

        if (baseNum.Length != 12)
            baseNum = baseNum.Substring(0, 12); // Обрезаем, если вдруг больше

        var bitmap = BarcodeGenerator.GenerateEan13(baseNum);
        AddBarcodeToCanvas(bitmap, "EAN13", baseNum, ElementType.Ean13);
    }

    private void AddDataMatrix()
    {
        var content = $"DM-X:{LabelWidth:F0},Y:{LabelHeight:F0}";
        var bitmap = BarcodeGenerator.GenerateDataMatrix(content);
        AddBarcodeToCanvas(bitmap, "DataMatrix", content, ElementType.DataMatrix);
    }

    private void AddCode128()
    {
        //var content = "00046070699704096210";
        //var content = $"[)>{LabelName}|{DateTime.Now:yyMMdd}|{LabelWidth}x{LabelHeight}";
        var content = $"00046070699704096210";
        var bitmap = BarcodeGenerator.GenerateCode128(content);
        AddBarcodeToCanvas(bitmap, "Code128", content, ElementType.Ean128);
    }

    // Вспомогательный метод: добавляет баркод как изображение на холст
    private void AddBarcodeToCanvas(Bitmap bitmap, string typeName, string data, ElementType type)
    {
        // Сохраняем оригинальный размер
        var originalWidth = bitmap.PixelSize.Width;
        var originalHeight = bitmap.PixelSize.Height;

        var imageControl = new Image
        {
            Source = bitmap,
            Width = originalWidth,
            Height = originalHeight,
            Stretch = Stretch.Uniform, // или UniformToFill, в зависимости от нужд
            IsHitTestVisible = true
        };

        double left = 50;
        double top = 50;

        Canvas.SetLeft(imageControl, left);
        Canvas.SetTop(imageControl, top);

        var elementVm = new ElementViewModel(
            imageControl,
            $"{typeName}_{Elements.Count + 1}",
            type,
            left, top,
            originalWidth,
            originalHeight,
            null
        )
        {
            Content = data, // Только текст! Не изображение
            Path = $"Generated:{typeName}='{data}'",
            OriginalImageData = bitmap // ← Добавь в ElementViewModel свойство для хранения оригинала
        };

        Elements.Add(elementVm);
        _mainWindow.LabelCanvas.Children.Add(imageControl);
        CurrentElement = elementVm;

        // --- Реактивная перегенерация ТОЛЬКО при изменении Content ---
        elementVm.WhenAnyValue(vm => vm.Content)
            .Where(content => !string.IsNullOrWhiteSpace(content))
            .Where(_ => !elementVm.IsEditing)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(async content =>
            {
                try
                {
                    // Перегенерируем ТОЛЬКО если контент изменился
                    Bitmap newBitmap = type switch
                    {
                        ElementType.QrCode => BarcodeGenerator.GenerateQrCode(content, (int)originalWidth, (int)originalHeight),
                        ElementType.Ean13 => BarcodeGenerator.GenerateEan13(content, (int)originalWidth, (int)originalHeight),
                        ElementType.DataMatrix => BarcodeGenerator.GenerateDataMatrix(content, (int)originalWidth, (int)originalHeight),
                        ElementType.Ean128 => BarcodeGenerator.GenerateCode128(content, (int)originalWidth, (int)originalHeight),
                        _ => throw new NotSupportedException("Unsupported barcode type")
                    };

                    imageControl.Source = newBitmap;

                    // Важно: размеры элемента остаются те же, но изображение заменяется
                    // Если хочешь, можно обновить OriginalImageData
                    elementVm.OriginalImageData = newBitmap;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка генерации баркода: {ex.Message}");
                }
            });

        // --- Привязка изменения размера элемента к размеру ImageControl ---
        // Когда пользователь меняет размер элемента — просто меняется размер Image, но не исходное изображение
        elementVm.WhenAnyValue(vm => vm.Width, vm => vm.Height)
            .Subscribe(_ =>
            {
                imageControl.Width = elementVm.Width;
                imageControl.Height = elementVm.Height;
            });
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