using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Nexplorer.App.Controls;

/// <summary>
/// An adorner that draws a semi-transparent selection rectangle (rubber-band)
/// over a list control to enable drag-to-select.
/// </summary>
public sealed class RubberBandAdorner : Adorner
{
    private Point _startPoint;
    private Point _endPoint;

    private static readonly Brush FillBrush = new SolidColorBrush(Color.FromArgb(40, 51, 153, 255));
    private static readonly Pen   BorderPen = new(new SolidColorBrush(Color.FromArgb(160, 51, 153, 255)), 1);

    static RubberBandAdorner()
    {
        FillBrush.Freeze();
        BorderPen.Freeze();
    }

    public RubberBandAdorner(UIElement adornedElement, Point startPoint) : base(adornedElement)
    {
        _startPoint = startPoint;
        _endPoint   = startPoint;
        IsHitTestVisible = false;
    }

    public Rect SelectionRect => new(
        Math.Min(_startPoint.X, _endPoint.X),
        Math.Min(_startPoint.Y, _endPoint.Y),
        Math.Abs(_endPoint.X - _startPoint.X),
        Math.Abs(_endPoint.Y - _startPoint.Y));

    public void UpdateEndPoint(Point endPoint)
    {
        var size = AdornedElement.RenderSize;
        _endPoint = new Point(
            Math.Clamp(endPoint.X, 0, size.Width),
            Math.Clamp(endPoint.Y, 0, size.Height));
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rect = SelectionRect;
        if (rect.Width > 0 && rect.Height > 0)
            drawingContext.DrawRectangle(FillBrush, BorderPen, rect);
    }
}
