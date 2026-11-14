using System.Windows;
using System.Windows.Media;

namespace CubeAudio.Controls;

public sealed class LevelBar : FrameworkElement
{
    private const double FloorDb = -60;
    private const double GoodFromDb = -18;
    private const double GoodToDb = -3;
    private const double LimitDb = -1.5;

    private Brush? _track;
    private Brush? _good;
    private Brush? _fill;
    private Brush? _limit;
    private Brush? _marker;
    private double _levelDb = FloorDb;
    private double _peakDb = FloorDb;

    public void Show(double levelDb, double peakDb)
    {
        if (levelDb == _levelDb && peakDb == _peakDb)
            return;

        _levelDb = levelDb;
        _peakDb = peakDb;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        _track ??= (Brush)FindResource("Text10");
        _good ??= (Brush)FindResource("PrimarySoft");
        _fill ??= (Brush)FindResource("Primary");
        _limit ??= (Brush)FindResource("Danger");
        _marker ??= (Brush)FindResource("Text");

        var width = ActualWidth;
        var height = ActualHeight;
        var radius = height / 2;

        drawing.PushClip(new RectangleGeometry(new Rect(0, 0, width, height), radius, radius));
        drawing.DrawRectangle(_track, null, new Rect(0, 0, width, height));
        drawing.DrawRectangle(_good, null, new Rect(Position(GoodFromDb) * width, 0, (Position(GoodToDb) - Position(GoodFromDb)) * width, height));
        drawing.DrawRectangle(_peakDb >= LimitDb ? _limit : _fill, null, new Rect(0, 0, Position(_levelDb) * width, height));
        drawing.Pop();

        if (_peakDb > FloorDb)
            drawing.DrawRectangle(_marker, null, new Rect(Math.Min(Position(_peakDb) * width, width - 2), 0, 2, height));
    }

    private static double Position(double db) => Math.Clamp((db - FloorDb) / -FloorDb, 0, 1);
}
