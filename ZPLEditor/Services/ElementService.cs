using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ZPLEditor.ViewModels;

namespace ZPLEditor.Services
{
    public class ElementService : IElementService
    {
        private Control? _draggedElement;
        private Point _startPoint;

        public ObservableCollection<ElementViewModel> Elements { get; } = new();
        public ElementViewModel? CurrentElement { get; set; }

        private readonly Canvas _canvas;

        public ElementService(Canvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        public void AddElement(ElementViewModel element, Control control, Canvas canvas)
        {
            if (!canvas.Children.Contains(control))
            {
                canvas.Children.Add(control);
            }

            if (!Elements.Contains(element))
            {
                Elements.Add(element);
            }

            SetCurrentElement(element);
        }

        public void RemoveElement(ElementViewModel element, Canvas canvas)
        {
            if (canvas.Children.Contains(element.Control))
            {
                canvas.Children.Remove(element.Control);
            }

            if (Elements.Contains(element))
            {
                Elements.Remove(element);
            }

            if (CurrentElement == element)
            {
                CurrentElement = null;
            }
        }

        public void ClearAll(Canvas canvas)
        {
            canvas.Children.Clear();
            Elements.Clear();
            CurrentElement = null;
        }

        public void SetCurrentElement(ElementViewModel? element)
        {
            CurrentElement = element;
        }

        public void BeginDrag(Control control, double x, double y)
        {
            _draggedElement = control;
            _startPoint = new Point(x, y);
        }

        public void DragTo(double x, double y)
        {
            if (_draggedElement == null) return;

            var pos = new Point(x, y);
            var delta = pos - _startPoint;

            var left = Canvas.GetLeft(_draggedElement) + delta.X;
            var top = Canvas.GetTop(_draggedElement) + delta.Y;

            Canvas.SetLeft(_draggedElement, left);
            Canvas.SetTop(_draggedElement, top);

            var vm = Elements.FirstOrDefault(e => e.Control == _draggedElement);
            if (vm != null)
            {
                vm.X = left;
                vm.Y = top;
            }

            _startPoint = pos;
        }

        public void EndDrag()
        {
            _draggedElement = null;
        }
    }
}