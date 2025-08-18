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
using System.Linq;
using System.Net.Sockets;
using System.Reactive;
using System.Xml.Linq;
using ZPLEditor.Models;
using ZPLEditor.Utils;
using ZPLEditor.Views;

namespace ZPLEditor.ViewModels;

public class MainViewModel : ViewModelBase
{

    private readonly MainView _mainWindow;

    // Список редактируемых элементов на холсте
    private readonly List<LabelElement> _labelElements = new();

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
        GenerateZplCommand = ReactiveCommand.Create(() =>
        {
            string zpl = ZPLUtils.GenerateZplFromControls(_labelElements);
            //ZPLUtils.PrintZPL(zpl);
        });


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

        var element = new LabelElement()
        {
            Name = textBox.Name ?? textBox.Text,
            Control = textBox
        };
        // Сохраняем ссылку
        _labelElements.Add(element);

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
            var element = new LabelElement();
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            element.Name = string.IsNullOrEmpty(fileName) ? "IMG1" : fileName;

            element.Data = File.ReadAllBytes(filePath);

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

            element.Control = imageControl;
            // Добавляем в список элементов
            _labelElements.Add(element);

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
            var control = parent as Control;

            if (_mainWindow.LabelCanvas.Children.Contains(control))
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

}