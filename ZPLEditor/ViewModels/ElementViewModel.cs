// File: ElementViewModel.cs
using ReactiveUI;
using Avalonia.Controls;

namespace ZPLEditor.ViewModels;

public class ElementViewModel : ReactiveObject
{
    private string _name;
    private double _x;
    private double _y;
    private double _width;
    private double _height;
    private byte[] _data;

    public Control Control { get; }

    public ElementViewModel(Control control, string name, double x, double y, double width, double height, byte[] data = null)
    {
        Control = control;
        _name = name;
        _x = x;
        _y = y;
        _width = width;
        _height = height;
        _data = data;
    }

    public string Name
    {
        get => _name;
        set
        {
            this.RaiseAndSetIfChanged(ref _name, value);
        }
    }

    public double X
    {
        get => _x;
        set
        {
            this.RaiseAndSetIfChanged(ref _x, value);
            Canvas.SetLeft(Control, value);
        }
    }

    public double Y
    {
        get => _y;
        set
        {
            this.RaiseAndSetIfChanged(ref _y, value);
            Canvas.SetTop(Control, value);
        }
    }

    public double Width
    {
        get => _width;
        set
        {
            this.RaiseAndSetIfChanged(ref _width, value);
            Control.Width = value;
        }
    }

    public double Height
    {
        get => _height;
        set
        {
            this.RaiseAndSetIfChanged(ref _height, value);
            Control.Height = value;
        }
    }
    public byte[] Data
    {
        get => _data;
        set
        {
            this.RaiseAndSetIfChanged(ref _data, value);
            _data = value;
        }
    }

    public void UpdateFromControl()
    {
        _x = Canvas.GetLeft(Control);
        _y = Canvas.GetTop(Control);
        _width = Control.Width;
        _height = Control.Height;
        this.RaisePropertyChanged(nameof(X));
        this.RaisePropertyChanged(nameof(Y));
        this.RaisePropertyChanged(nameof(Width));
        this.RaisePropertyChanged(nameof(Height));
    }
}