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
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reactive;
using System.Xml.Linq;
using ZPLEditor.Views;

namespace ZPLEditor.ViewModels;

public class MainViewModel : ViewModelBase
{

    byte[] imageBytes = null;

    // Теперь у вас есть и bitmap, и исходные байты




    private readonly MainView _mainWindow;

    // Список редактируемых элементов на холсте
    private readonly List<Control> _labelElements = new();

    // Активный элемент для перетаскивания
    private Control _draggedElement;
    private Point _startPoint;

    // Команды
    public ReactiveCommand<Unit, Unit> AddTextCommand { get; }
    public ReactiveCommand<Unit, Unit> AddImageCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateZplCommand { get; }

    // Свойства представления
    [Reactive] public string ZplOutput { get; set; } = "";

    // Конструктор
    public MainViewModel(MainView mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        AddTextCommand = ReactiveCommand.Create(AddTextBox);
        AddImageCommand = ReactiveCommand.Create(AddImage);
        GenerateZplCommand = ReactiveCommand.Create(GenerateZpl);

        // Подписываемся только на события холста
        _mainWindow.LabelCanvas.AddHandler(
            InputElement.PointerPressedEvent,
            Canvas_PointerPressed,
            RoutingStrategies.Tunnel);


        _mainWindow.LabelCanvas.PointerMoved += HandlePointerMoved;
        _mainWindow.LabelCanvas.PointerReleased += HandlePointerReleased;
    }

    #region Работа с элементами на холсте


    /// <summary>
    /// Добавляет новый редактируемый текстовый элемент (TextBox) на холст.
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

        // События
        textBox.DoubleTapped += (s, e) =>
        {
            textBox.Focus();
            e.Handled = true;
        };

        textBox.GotFocus += (s, e) =>
        {
            // Если элемент был в процессе перетаскивания — отменяем
            if (_draggedElement == textBox)
            {
                _draggedElement = null;
            }
        };

        // Сохраняем ссылку
        _labelElements.Add(textBox);

        // Добавляем на холст
        _mainWindow.LabelCanvas.Children.Add(textBox);
    }


    /// <summary>
    /// Добавляет новый редактируемый текстовый элемент (TextBox) на холст.
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

            imageBytes = File.ReadAllBytes(filePath);

            var imageControl = new Image
            {
                Source = bitmap,
                Width = bitmap.PixelSize.Width,
                Height = bitmap.PixelSize.Height,
                Stretch = Stretch.None, // Чтобы не растягивалось
                IsHitTestVisible = true // Чтобы можно было кликать и перетаскивать
            };

            // Начальная позиция
            Canvas.SetLeft(imageControl, 50);
            Canvas.SetTop(imageControl, 50);


            // Добавляем в список элементов
            _labelElements.Add(imageControl);

            // Добавляем на холст
            _mainWindow.LabelCanvas.Children.Add(imageControl);
        }
        catch (Exception ex)
        {
            // Можно показать MessageBox, но здесь просто лог
            Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
        }
    }


    /// <summary>
    /// Обработка нажатия на холст — определяем, по какому элементу кликнули.
    /// </summary>
    private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        var pos = e.GetPosition(_mainWindow.LabelCanvas);
        var hitControl = _mainWindow.LabelCanvas.InputHitTest(pos) as Avalonia.Controls.Control;

        Control actualElement = null;
        var parent = hitControl;
        while (parent != null)
        {
            if (_labelElements.Contains(parent as Control))
            {
                actualElement = parent as Control;
                break;
            }
            parent = (Control)parent.Parent;
        }

        if (actualElement != null)
        {
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
                    {
                        tb.Focus();
                    }
                    e.Handled = true;
                }
            }
        }
    }

    /// <summary>
    /// Обработка движения мыши при зажатой кнопке.
    /// </summary>
    private void HandlePointerMoved(object sender, PointerEventArgs e)
    {
        if (_draggedElement == null) return;

        var pos = e.GetPosition(_mainWindow.LabelCanvas);
        var delta = pos - _startPoint;

        var left = Canvas.GetLeft(_draggedElement) + delta.X;
        var top = Canvas.GetTop(_draggedElement) + delta.Y;

        //left = Math.Max(0, left);
        //top = Math.Max(0, top);

        Canvas.SetLeft(_draggedElement, left);
        Canvas.SetTop(_draggedElement, top);

        _startPoint = pos;
    }

    /// <summary>
    /// Завершение перетаскивания.
    /// </summary>
    private void HandlePointerReleased(object sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        _draggedElement = null;
    }

    #endregion

    #region Генерация ZPL

    /// <summary>
    /// Генерирует ZPL-код на основе элементов на холсте и отправляет на принтер.
    /// </summary>
    private void GenerateZpl()
    {
        var zplElements = new List<ZplElementBase>();

        foreach (var child in _mainWindow.LabelCanvas.Children)
        {
            if (child is TextBox tb)
            {
                var x = (int)Canvas.GetLeft(tb);
                var y = (int)Canvas.GetTop(tb);
                var font = new ZplFont(fontWidth: 50, fontHeight: 50);
                zplElements.Add(new ZplTextField(tb.Text, x, y, font));
            }
            else if (child is TextBlock tl)
            {
                var x = (int)Canvas.GetLeft(tl);
                var y = (int)Canvas.GetTop(tl);
                var font = new ZplFont(fontWidth: 50, fontHeight: 50);
                zplElements.Add(new ZplTextField(tl.Text, x, y, font));
            }
            else if (child is Image img && img.Source is Bitmap bitmap)
            {
                var x = (int)Canvas.GetLeft(img);
                var y = (int)Canvas.GetTop(img);

                try
                {
                    zplElements.Add(new ZplDownloadGraphics('R', "SAMPLE", imageBytes));
                    zplElements.Add(new ZplRecallGraphic(x, y, 'R', "SAMPLE"));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка конвертации изображения: {ex.Message}");
                }
            }
        }

        var engine = new ZplEngine(zplElements);
        var options = new ZplRenderOptions { AddEmptyLineBeforeElementStart = true, TargetPrintDpi = 304, SourcePrintDpi = 304 };
        ZplOutput = engine.ToZplString(options);

        Debug.WriteLine(engine.ToZplString(options));

        PrintZPL();


    }


    /// <summary>
    /// Отправляет ZPL-код на принтер по TCP.
    /// </summary>
    private void PrintZPL()
    {
        const string printerIp = "192.168.10.202";
        const int printerPort = 9100;

        try
        {
            using var client = new TcpClient(printerIp, printerPort);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream) { AutoFlush = true };
            writer.Write(ZplOutput);
            Debug.WriteLine("ZPL отправлен на принтер.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Ошибка печати: {ex.Message}");
        }
    }
    /// <summary>
    /// Конвертирует Bitmap в монохромную hex-строку для ZPL (формат ^GF).
    /// </summary>
    private string BitmapToZplHex(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory);
        memory.Position = 0;

        using var bmp = new System.Drawing.Bitmap(memory);
        int width = bmp.Width;
        int height = bmp.Height;
        int byteWidth = (width + 7) / 8;
        var bytes = new byte[byteWidth * height];

        for (int y = 0; y < height; y++)
        {
            for (int xByte = 0; xByte < byteWidth; xByte++)
            {
                byte b = 0;
                for (int bit = 0; bit < 8; bit++)
                {
                    int x = xByte * 8 + bit;
                    if (x >= width) break;
                    var pixel = bmp.GetPixel(x, y);
                    int brightness = (pixel.R + pixel.G + pixel.B) / 3;
                    if (brightness < 128)
                    {
                        b |= (byte)(0x80 >> bit);
                    }
                }
                bytes[y * byteWidth + xByte] = b;
            }
        }

        return BitConverter.ToString(bytes).Replace("-", "");
    }

    #endregion
}