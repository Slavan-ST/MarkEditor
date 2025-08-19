// File: ElementViewModel.cs
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;

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
        string path = "")
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

        // Подписываемся на изменения контролов и обновляем VM
        Observable
            .FromEventPattern<EventHandler, EventArgs>(
                h => Control.LayoutUpdated += h,
                h => Control.LayoutUpdated -= h)
            .Subscribe(_ => UpdateFromControl())
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
    [Reactive] public string Content { get; set; } = string.Empty;

    // Свойства видимости (реактивные, но управляются логикой)
    [Reactive] public bool IsContentVisible { get; set; }
    [Reactive] public bool IsPathVisible { get; set; }

    /// <summary>
    /// Обновляет флаги видимости в зависимости от типа элемента.
    /// </summary>
    private void UpdateVisibilityFlags(ElementType type)
    {
        IsContentVisible = type != ElementType.Image;
        IsPathVisible = type == ElementType.Image;
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

    public void UpdateContent(string newContent)
    {
        if (IsEditing) return; // Защита от рекурсии
        Content = newContent;
    }

    // Освобождение подписок
    public void Dispose()
    {
        _disposables?.Dispose();
    }
}