using System.Windows;
using System.Windows.Controls;

namespace Nexplorer.App.Controls;

/// <summary>
/// A panel that arranges children using the squarified treemap algorithm.
/// Each child must have the TreemapPanel.Weight attached property set.
/// </summary>
public sealed class TreemapPanel : Panel
{
    public static readonly DependencyProperty WeightProperty =
        DependencyProperty.RegisterAttached("Weight", typeof(double), typeof(TreemapPanel),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static void SetWeight(UIElement element, double value) => element.SetValue(WeightProperty, value);
    public static double GetWeight(UIElement element) => (double)element.GetValue(WeightProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren)
            child.Measure(availableSize);
        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0 || finalSize.Width <= 0 || finalSize.Height <= 0)
            return finalSize;

        var items = new List<(UIElement Element, double Weight)>(InternalChildren.Count);
        foreach (UIElement child in InternalChildren)
        {
            double w = GetWeight(child);
            if (w > 0) items.Add((child, w));
            else child.Arrange(new Rect(0, 0, 0, 0));
        }

        if (items.Count == 0) return finalSize;

        items.Sort((a, b) => b.Weight.CompareTo(a.Weight));
        Squarify(items, new Rect(0, 0, finalSize.Width, finalSize.Height));
        return finalSize;
    }

    private static void Squarify(List<(UIElement Element, double Weight)> items, Rect bounds)
    {
        double totalWeight = 0;
        foreach (var (_, w) in items) totalWeight += w;
        if (totalWeight <= 0) return;

        LayoutStrip(items, 0, items.Count, bounds, totalWeight);
    }

    private static void LayoutStrip(List<(UIElement Element, double Weight)> items,
        int start, int end, Rect bounds, double totalWeight)
    {
        if (start >= end) return;
        if (end - start == 1)
        {
            items[start].Element.Arrange(new Rect(bounds.X, bounds.Y,
                Math.Max(0, bounds.Width), Math.Max(0, bounds.Height)));
            return;
        }

        double totalArea = bounds.Width * bounds.Height;
        bool isWide = bounds.Width >= bounds.Height;

        double stripWeight = 0;
        double bestAspect = double.MaxValue;
        int splitIndex = start + 1;

        for (int i = start; i < end; i++)
        {
            stripWeight += items[i].Weight;
            double stripFraction = stripWeight / totalWeight;

            double stripSize = isWide
                ? bounds.Width * stripFraction
                : bounds.Height * stripFraction;

            double worst = WorstAspectInStrip(items, start, i + 1, stripWeight,
                totalArea, isWide ? bounds.Height : bounds.Width, stripSize);

            if (worst < bestAspect)
            {
                bestAspect = worst;
                splitIndex = i + 1;
            }
            else break;
        }

        double splitStripWeight = 0;
        for (int i = start; i < splitIndex; i++) splitStripWeight += items[i].Weight;

        double fraction = splitStripWeight / totalWeight;

        Rect stripBounds, remaining;
        if (isWide)
        {
            double w = bounds.Width * fraction;
            stripBounds = new Rect(bounds.X, bounds.Y, w, bounds.Height);
            remaining = new Rect(bounds.X + w, bounds.Y, bounds.Width - w, bounds.Height);
        }
        else
        {
            double h = bounds.Height * fraction;
            stripBounds = new Rect(bounds.X, bounds.Y, bounds.Width, h);
            remaining = new Rect(bounds.X, bounds.Y + h, bounds.Width, bounds.Height - h);
        }

        ArrangeRow(items, start, splitIndex, stripBounds, splitStripWeight, isWide);

        double remainingWeight = totalWeight - splitStripWeight;
        if (remainingWeight > 0 && splitIndex < end)
            LayoutStrip(items, splitIndex, end, remaining, remainingWeight);
    }

    private static double WorstAspectInStrip(List<(UIElement Element, double Weight)> items,
        int start, int end, double stripWeight, double totalArea, double stripLength, double stripSize)
    {
        double worst = 0;
        for (int i = start; i < end; i++)
        {
            double itemFrac = items[i].Weight / stripWeight;
            double itemLen = stripLength * itemFrac;
            if (itemLen <= 0 || stripSize <= 0) continue;
            double aspect = itemLen > stripSize ? itemLen / stripSize : stripSize / itemLen;
            if (aspect > worst) worst = aspect;
        }
        return worst;
    }

    private static void ArrangeRow(List<(UIElement Element, double Weight)> items,
        int start, int end, Rect bounds, double stripWeight, bool isWide)
    {
        double offset = 0;
        for (int i = start; i < end; i++)
        {
            double frac = items[i].Weight / stripWeight;
            Rect r;
            if (isWide)
            {
                double h = bounds.Height * frac;
                r = new Rect(bounds.X, bounds.Y + offset, Math.Max(0, bounds.Width), Math.Max(0, h));
                offset += h;
            }
            else
            {
                double w = bounds.Width * frac;
                r = new Rect(bounds.X + offset, bounds.Y, Math.Max(0, w), Math.Max(0, bounds.Height));
                offset += w;
            }
            items[i].Element.Arrange(r);
        }
    }
}
