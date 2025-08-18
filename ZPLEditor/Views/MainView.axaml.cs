using Avalonia.Controls;
using Avalonia.Input;
using BinaryKits.Zpl.Viewer;
using System.Drawing;
using System.IO;
using ZPLEditor.ViewModels;

namespace ZPLEditor.Views;

public partial class MainView : UserControl
{
    private readonly MainViewModel _viewModel;

    public MainView()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(this);
        DataContext = _viewModel;
    }

    private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        // Снимаем выделение при клике мимо элементов
    }
}
