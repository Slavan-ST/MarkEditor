// File: ElementViewModel.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ZPLEditor.Utils;

namespace ZPLEditor.ViewModels;

/// <summary>
/// Типы элементов, которые могут быть добавлены на холст.
/// </summary>
public enum ElementType
{
    Text,
    Ean13,
    Ean128,
    QrCode,
    Image,
    DataMatrix
}

/// <summary>
/// ViewModel для элемента на холсте (текст, баркод, изображение и т.д.).
/// Связывает данные с UI-контролом и обеспечивает реактивное поведение.
/// </summary>
public class ElementViewModel : ReactiveObject, IDisposable
{
    #region Поля

    private readonly CompositeDisposable _disposables = new();

    #endregion

    #region Свойства (Reactive)

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

    // Свойства видимости (управляются типом элемента)
    [Reactive] public bool IsContentVisible { get; set; }
    [Reactive] public bool IsPathVisible { get; set; }
    [Reactive] public bool IsFontSizeVisible { get; set; }
    [Reactive] public bool IsRotationVisible { get; set; }

    // Оригинальные размеры (для изображений)
    [Reactive] public double OriginalWidth { get; set; }
    [Reactive] public double OriginalHeight { get; set; }

    // Масштаб и трансформации
    [Reactive] public double ScaleX { get; set; } = 1.0;
    [Reactive] public double ScaleY { get; set; } = 1.0;
    [Reactive] public double FontSize { get; set; } = 12.0;
    [Reactive] public double Rotation { get; set; } = 0.0;

    #endregion

    #region Связанные объекты

    /// <summary>
    /// UI-контрол, связанный с этим элементом.
    /// </summary>
    public Control Control { get; }

    #endregion

    #region Конструкторы

    public ElementViewModel()
    {
    }

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
        Path = path;
        OriginalImageData = originalImageData;

        // Инициализация оригинальных размеров и масштаба
        if (OriginalImageData != null)
        {
            OriginalWidth = OriginalImageData.PixelSize.Width;
            OriginalHeight = OriginalImageData.PixelSize.Height;
            ScaleX = Width / OriginalWidth;
            ScaleY = Height / OriginalHeight;
        }
        else
        {
            OriginalWidth = Width;
            OriginalHeight = Height;
            ScaleX = 1.0;
            ScaleY = 1.0;
        }

        // Установка начальных значений на контроле
        Canvas.SetLeft(Control, X);
        Canvas.SetTop(Control, Y);
        Control.Width = Width;
        Control.Height = Height;

        // Привязки: VM -> Control
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

        // Обновление ScaleX при изменении Width
        this.WhenAnyValue(vm => vm.Width, vm => vm.OriginalWidth)
            .Where(tuple => tuple.Item2 > 0)
            .Select(tuple => tuple.Item1 / tuple.Item2)
            .BindTo(this, vm => vm.ScaleX)
            .DisposeWith(_disposables);

        // Обновление ScaleY при изменении Height
        this.WhenAnyValue(vm => vm.Height, vm => vm.OriginalHeight)
            .Where(tuple => tuple.Item2 > 0)
            .Select(tuple => tuple.Item1 / tuple.Item2)
            .BindTo(this, vm => vm.ScaleY)
            .DisposeWith(_disposables);

        // Привязка поворота
        this.WhenAnyValue(vm => vm.Rotation)
            .Subscribe(angle =>
            {
                Control.RenderTransform = new RotateTransform(angle);
                Control.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            })
            .DisposeWith(_disposables);

        // Привязка размера шрифта
        this.WhenAnyValue(vm => vm.FontSize)
            .Where(size => size > 0)
            .Subscribe(size =>
            {
                if (Control is TextBox textElement)
                {
                    textElement.FontSize = size;
                }
            })
            .DisposeWith(_disposables);

        // Обновление видимости на основе типа элемента
        this.WhenAnyValue(x => x.Type)
            .Subscribe(UpdateVisibilityFlags)
            .DisposeWith(_disposables);

        // Обратная привязка: Control -> VM (LayoutUpdated)
        Observable
            .FromEventPattern<EventHandler, EventArgs>(
                h => Control.LayoutUpdated += h,
                h => Control.LayoutUpdated -= h)
            .Subscribe(_ => UpdateFromControl())
            .DisposeWith(_disposables);
    }

    #endregion

    #region Методы

    /// <summary>
    /// Обновляет флаги видимости в зависимости от типа элемента.
    /// </summary>
    private void UpdateVisibilityFlags(ElementType type)
    {
        IsContentVisible = type != ElementType.Image;
        IsPathVisible = type == ElementType.Image;
        IsFontSizeVisible = type is ElementType.Text or
                                 ElementType.Ean13 or
                                 ElementType.Ean128 or
                                 ElementType.QrCode or
                                 ElementType.DataMatrix;
        IsRotationVisible = true;
    }

    /// <summary>
    /// Обновляет свойства X, Y, Width, Height из текущего состояния Control.
    /// Вызывается при изменении layout (например, при drag & resize).
    /// </summary>
    private void UpdateFromControl()
    {
        var newX = Canvas.GetLeft(Control);
        var newY = Canvas.GetTop(Control);
        var newWidth = Control.Width;
        var newHeight = Control.Height;

        if (!X.Equals(newX)) X = newX;
        if (!Y.Equals(newY)) Y = newY;
        if (!Width.Equals(newWidth)) Width = newWidth;
        if (!Height.Equals(newHeight)) Height = newHeight;
    }

    /// <summary>
    /// Масштабирует все элементы в коллекции по заданному коэффициенту,
    /// используя оригинальные изображения высокого разрешения для точного пересчёта размеров.
    /// </summary>
    /// <param name="elements">Коллекция элементов для масштабирования.</param>
    /// <param name="scaleFactor">Коэффициент масштабирования (например, 1.0, 1.5, 2.0 и т.д.)</param>
    public static IEnumerable<ElementViewModel> ScaleElements(IEnumerable<ElementViewModel> elements, double scaleFactor)
    {
        foreach (var element in elements)
        {
            var scaledElement = new ElementViewModel
            {
                X = element.X * scaleFactor,
                Y = element.Y * scaleFactor,
                Width = element.Width,
                Height = element.Height,
                OriginalWidth = element.OriginalWidth,
                OriginalHeight = element.OriginalHeight,
                ScaleX = element.ScaleX,
                ScaleY = element.ScaleY,
                Type = element.Type,
                Content = element.Content,
                IsFontSizeVisible = element.IsFontSizeVisible,
                FontSize = element.IsFontSizeVisible ? element.FontSize * scaleFactor : element.FontSize,
                Rotation = element.Rotation,
                OriginalImageData = element.OriginalImageData
            };

            if (scaledElement.OriginalImageData != null && scaledElement.OriginalWidth > 0 && scaledElement.OriginalHeight > 0)
            {
                scaledElement.Width = scaledElement.OriginalWidth * scaledElement.ScaleX * scaleFactor;
                scaledElement.Height = scaledElement.OriginalHeight * scaledElement.ScaleY * scaleFactor;

                // Перегенерация баркода при масштабировании
                if (scaledElement.Type == ElementType.Ean128)
                {
                    scaledElement.OriginalImageData = BarcodeGenerator.GenerateCode128(scaledElement.Content, (int)scaledElement.Width, (int)scaledElement.Height);
                }
                else if (scaledElement.Type == ElementType.Ean13)
                {
                    scaledElement.OriginalImageData = BarcodeGenerator.GenerateEan13(scaledElement.Content, (int)scaledElement.Width, (int)scaledElement.Height);
                }
            }
            else
            {
                scaledElement.Width = element.Width * scaleFactor;
                scaledElement.Height = element.Height * scaleFactor;
            }

            yield return scaledElement;
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Освобождает ресурсы, связанные с подписками.
    /// </summary>
    public void Dispose()
    {
        _disposables?.Dispose();
    }

    #endregion
}