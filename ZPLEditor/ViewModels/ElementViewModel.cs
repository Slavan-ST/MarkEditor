// File: ElementViewModel.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ZPLEditor.ViewModels;

// Перечисление типов элементов
public enum ElementType
{
    Text,
    Ean13,
    Ean128,
    QrCode,
    Image,
    DataMatrix
}

public class ElementViewModel : ReactiveObject, IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public Control Control { get; }

    // Конструктор
    public ElementViewModel(
        Control control,
        string name,
        ElementType type,
        double x,
        double y,
        double width,
        double height,
        byte[]? data = null,
        string content = "",
        string path = "",
        Bitmap? originalImageData = null)
    {
        Control = control;
        Name = name;
        Type = type;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Data = data;
        Content = content;
        Path = path; // Установим из параметра
        OriginalImageData = originalImageData;

        // Если это баркод или изображение — сохраняем оригинальные размеры
        if (OriginalImageData != null)
        {
            OriginalWidth = OriginalImageData.PixelSize.Width;
            OriginalHeight = OriginalImageData.PixelSize.Height;

            // Инициализируем масштаб
            ScaleX = Width / OriginalWidth;
            ScaleY = Height / OriginalHeight;
        }
        else
        {
            // Для текста или других элементов без изображения
            OriginalWidth = Width;
            OriginalHeight = Height;
            ScaleX = 1.0;
            ScaleY = 1.0;
        }

        // Устанавливаем начальные значения на контроле
        Canvas.SetLeft(Control, X);
        Canvas.SetTop(Control, Y);
        Control.Width = Width;
        Control.Height = Height;

        // Подписываемся на изменения свойств контролов
        this.WhenAnyValue(vm => vm.X)
            .Subscribe(x => Canvas.SetLeft(Control, x))
            .DisposeWith(_disposables);

        this.WhenAnyValue(vm => vm.Y)
            .Subscribe(y => Canvas.SetTop(Control, y))
            .DisposeWith(_disposables);

        this.WhenAnyValue(vm => vm.Width)
            .Subscribe(width => Control.Width = width)
            .DisposeWith(_disposables);

        this.WhenAnyValue(vm => vm.Height)
            .Subscribe(height => Control.Height = height)
            .DisposeWith(_disposables);

        // Обновляем ScaleX при изменении Width
        this.WhenAnyValue(vm => vm.Width, vm => vm.OriginalWidth)
            .Where(tuple => tuple.Item2 > 0) // Проверяем, что OriginalWidth > 0
            .Select(tuple => tuple.Item1 / tuple.Item2)
            .BindTo(this, vm => vm.ScaleX)
            .DisposeWith(_disposables);

        // Обновляем ScaleY при изменении Height
        this.WhenAnyValue(vm => vm.Height, vm => vm.OriginalHeight)
            .Where(tuple => tuple.Item2 > 0)
            .Select(tuple => tuple.Item1 / tuple.Item2)
            .BindTo(this, vm => vm.ScaleY)
            .DisposeWith(_disposables);

        // Подписываемся на изменения контролов и обновляем VM --@@ надо проверить это, и поправить
        Observable
            .FromEventPattern<EventHandler, EventArgs>(
                h => Control.LayoutUpdated += h,
                h => Control.LayoutUpdated -= h)
            .Subscribe(_ => UpdateFromControl())
            .DisposeWith(_disposables);
        // Привязка Rotation
        this.WhenAnyValue(vm => vm.Rotation)
            .Subscribe(angle =>
            {
                Control.RenderTransform = new RotateTransform(angle);
                Control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative); // вращение вокруг центра
            })
            .DisposeWith(_disposables);
        // Привязка FontSize
        this.WhenAnyValue(vm => vm.FontSize)
            .Where(size => size > 0)
            .Subscribe(size =>
            {
                if (Control is TextBox textElement)
                {
                    textElement.FontSize = size;
                }
                // Или через прямое присваивание, если Control — TextBlock, Button и т.п.
                // Например: (Control as TextBlock)?.SetFontValue(TextBlock.FontSizeProperty, size);
            })
            .DisposeWith(_disposables);

        // Реактивно вычисляем видимость на основе Type
        this.WhenAnyValue(x => x.Type)
            .Subscribe(UpdateVisibilityFlags)
            .DisposeWith(_disposables);
    }

    [Reactive] public bool IsEditing { get; set; }
    [Reactive] public string Name { get; set; } = string.Empty;
    [Reactive] public string Path { get; set; } = string.Empty;
    [Reactive] public ElementType Type { get; set; }
    [Reactive] public double X { get; set; }
    [Reactive] public double Y { get; set; }
    [Reactive] public double Width { get; set; }
    [Reactive] public double Height { get; set; }
    [Reactive] public byte[]? Data { get; set; }
    [Reactive] public Bitmap? OriginalImageData { get; set; }
    [Reactive] public string Content { get; set; } = string.Empty;

    // Свойства видимости (реактивные, но управляются логикой)
    [Reactive] public bool IsContentVisible { get; set; }
    [Reactive] public bool IsPathVisible { get; set; }

    [Reactive] public double OriginalWidth { get; set; }  // Только для чтения, устанавливается при создании
    [Reactive] public double OriginalHeight { get; set; } // Только для чтения, устанавливается при создании

    [Reactive] public double ScaleX { get; set; } = 1.0; // По умолчанию 1.0
    [Reactive] public double ScaleY { get; set; } = 1.0; // По умолчанию 1.0
    [Reactive] public double FontSize { get; set; } = 12.0;

    [Reactive] public double Rotation { get; set; } = 0.0; //угол поворота элемента

    [Reactive] public bool IsFontSizeVisible { get; set; }
    [Reactive] public bool IsRotationVisible { get; set; } // Можно показывать всегда или по условию


    /// <summary>
    /// Обновляет флаги видимости в зависимости от типа элемента.
    /// </summary>
    private void UpdateVisibilityFlags(ElementType type)
    {
        // Контент виден у всех, кроме изображения
        IsContentVisible = type != ElementType.Image;

        // Путь виден только у изображения
        IsPathVisible = type == ElementType.Image;

        // Размер шрифта виден у текста и баркодов
        IsFontSizeVisible = type == ElementType.Text ||
                            type == ElementType.Ean13 ||
                            type == ElementType.Ean128 ||
                            type == ElementType.QrCode ||
                            type == ElementType.DataMatrix;

        // Поворот можно разрешить для всех, или ограничить
        IsRotationVisible = true; // или, например, type != ElementType.Image, если не нужно
    }

    /// <summary>
    /// Обновляет X, Y, Width, Height из текущего состояния Control.
    /// Вызывается при изменении layout (например, при drag & resize).
    /// </summary>
    private void UpdateFromControl()
    {
        var newX = Canvas.GetLeft(Control);
        var newY = Canvas.GetTop(Control);
        var newWidth = Control.Width;
        var newHeight = Control.Height;

        // Обновляем только при изменении, чтобы избежать лишних уведомлений
        if (!X.Equals(newX)) X = newX;
        if (!Y.Equals(newY)) Y = newY;
        if (!Width.Equals(newWidth)) Width = newWidth;
        if (!Height.Equals(newHeight)) Height = newHeight;
    }

    /// <summary>
    /// Применяет масштаб ко всем элементам: подгружает оригинальные изображения при необходимости и масштабирует размеры и позиции.
    /// </summary>
    /// <param name="elements">Коллекция элементов для масштабирования.</param>
    /// <param name="scale">Масштаб (например, 1.0 = 100%, 2.0 = 200%).</param>
    /// <returns>Коллекция обновлённых элементов.</returns>
    public static async Task<IEnumerable<ElementViewModel>> ApplyScaleAsync(
        IEnumerable<ElementViewModel> elements,
        double scale)
    {
        var tasks = elements.Select(async element =>
        {
            // 1. Подгружаем OriginalImageData, если он null и элемент — изображение
            if (element.Type == ElementType.Image && element.OriginalImageData == null && !string.IsNullOrEmpty(element.Path))
            {
                try
                {
                    var bitmap = await Task.Run(() => new Bitmap(element.Path));
                    element.OriginalImageData = bitmap;
                    element.OriginalWidth = bitmap.PixelSize.Width;
                    element.OriginalHeight = bitmap.PixelSize.Height;

                    // Пересчитываем текущие размеры, если нужно
                    if (element.Width == 0 || element.Height == 0)
                    {
                        element.Width = bitmap.PixelSize.Width;
                        element.Height = bitmap.PixelSize.Height;
                    }
                }
                catch (Exception ex)
                {
                    // Логирование (если есть) или пропуск
                    Console.WriteLine($"Failed to load image: {element.Path}, Error: {ex.Message}");
                    // Оставляем как есть
                }
            }

            // 2. Применяем масштабирование к позиции и размерам
            element.X *= scale;
            element.Y *= scale;
            element.Width *= scale;
            element.Height *= scale;

            // 3. Пересчитываем ScaleX и ScaleY, если есть оригинальное изображение
            if (element.OriginalImageData != null && element.OriginalWidth > 0 && element.OriginalHeight > 0)
            {
                element.ScaleX = element.Width / element.OriginalWidth;
                element.ScaleY = element.Height / element.OriginalHeight;
            }

            // 4. Обновляем контроль (UI)
            Canvas.SetLeft(element.Control, element.X);
            Canvas.SetTop(element.Control, element.Y);
            element.Control.Width = element.Width;
            element.Control.Height = element.Height;

            return element;
        });

        return await Task.WhenAll(tasks);
    }


    // Освобождение подписок
    public void Dispose()
    {
        _disposables?.Dispose();
    }


}