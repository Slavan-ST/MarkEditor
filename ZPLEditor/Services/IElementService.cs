using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using MarkEditor.ViewModels;

namespace MarkEditor.Services;

/// <summary>
/// Сервис для управления элементами на холсте.
/// </summary>
public interface IElementService
{
    ObservableCollection<ElementViewModel> Elements { get; }

    ElementViewModel? CurrentElement { get; set; }

    void AddElement(ElementViewModel element, Control control, Canvas canvas);
    void RemoveElement(ElementViewModel element, Canvas canvas);
    void ClearAll(Canvas canvas);
    void SetCurrentElement(ElementViewModel? element);
    void BeginDrag(Control control, double x, double y);
    void DragTo(double x, double y);
    void EndDrag();
}