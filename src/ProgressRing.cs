using System;
using System.Windows;
using System.Windows.Media;

namespace TomatoClock
{
    /// <summary>
    /// A lightweight circular countdown ring with a ticked outer scale.
    /// </summary>
    public sealed class ProgressRing : FrameworkElement
    {
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(ProgressRing),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender,
                    null, new CoerceValueCallback(CoerceProgress)));

        public static readonly DependencyProperty ThicknessProperty =
            DependencyProperty.Register("Thickness", typeof(double), typeof(ProgressRing),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register("TrackBrush", typeof(Brush), typeof(ProgressRing),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ProgressBrushProperty =
            DependencyProperty.Register("ProgressBrush", typeof(Brush), typeof(ProgressRing),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TickBrushProperty =
            DependencyProperty.Register("TickBrush", typeof(Brush), typeof(ProgressRing),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public ProgressRing()
        {
            SnapsToDevicePixels = false;
            IsHitTestVisible = false;
        }

        public double Progress
        {
            get { return (double)GetValue(ProgressProperty); }
            set { SetValue(ProgressProperty, value); }
        }

        public double Thickness
        {
            get { return (double)GetValue(ThicknessProperty); }
            set { SetValue(ThicknessProperty, value); }
        }

        public Brush TrackBrush
        {
            get { return (Brush)GetValue(TrackBrushProperty); }
            set { SetValue(TrackBrushProperty, value); }
        }

        public Brush ProgressBrush
        {
            get { return (Brush)GetValue(ProgressBrushProperty); }
            set { SetValue(ProgressBrushProperty, value); }
        }

        public Brush TickBrush
        {
            get { return (Brush)GetValue(TickBrushProperty); }
            set { SetValue(TickBrushProperty, value); }
        }

        private static object CoerceProgress(DependencyObject d, object baseValue)
        {
            double value = (double)baseValue;
            if (value < 0.0) return 0.0;
            if (value > 1.0) return 1.0;
            return value;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 220.0 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? 220.0 : availableSize.Height;
            return new Size(width, height);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            if (width < 8.0 || height < 8.0) return;

            Point center = new Point(width / 2.0, height / 2.0);
            double outer = Math.Min(width, height) / 2.0;
            double thickness = Math.Max(2.0, Thickness);
            double radius = outer - thickness / 2.0 - 13.0;
            if (radius <= 2.0) return;

            Brush track = TrackBrush ?? new SolidColorBrush(Color.FromArgb(80, 208, 185, 158));
            Brush accent = ProgressBrush ?? new SolidColorBrush(Color.FromRgb(226, 100, 76));
            Brush tick = TickBrush ?? new SolidColorBrush(Color.FromArgb(44, 130, 105, 82));

            // Outer tick scale: 60 small marks, longer every five.
            Pen minorTickPen = new Pen(tick, 0.9);
            Pen majorTickPen = new Pen(tick, 1.35);
            double tickStart = radius + thickness / 2.0 + 5.0;
            for (int i = 0; i < 60; i++)
            {
                double angle = (i * 6.0 - 90.0) * Math.PI / 180.0;
                bool major = (i % 5 == 0);
                double length = major ? 4.2 : 2.1;
                Point p1 = PointOnCircle(center, tickStart, angle);
                Point p2 = PointOnCircle(center, tickStart + length, angle);
                dc.DrawLine(major ? majorTickPen : minorTickPen, p1, p2);
            }

            Pen trackPen = new Pen(track, thickness);
            trackPen.StartLineCap = PenLineCap.Round;
            trackPen.EndLineCap = PenLineCap.Round;
            dc.DrawEllipse(null, trackPen, center, radius, radius);

            double progress = Progress;
            if (progress <= 0.0004) return;

            double sweep = progress * 360.0;
            Pen accentPen = new Pen(accent, thickness);
            accentPen.StartLineCap = PenLineCap.Round;
            accentPen.EndLineCap = PenLineCap.Round;

            if (progress >= 0.9996)
            {
                dc.DrawEllipse(null, accentPen, center, radius, radius);
                return;
            }

            // A barely-there halo makes the colored arc feel warm rather than flat.
            Brush haloBrush = accent.Clone();
            haloBrush.Opacity = 0.10;
            haloBrush.Freeze();
            Pen haloPen = new Pen(haloBrush, thickness * 2.4);
            haloPen.StartLineCap = PenLineCap.Round;
            haloPen.EndLineCap = PenLineCap.Round;
            dc.DrawGeometry(null, haloPen, CreateArc(center, radius, -90.0, sweep));

            dc.DrawGeometry(null, accentPen, CreateArc(center, radius, -90.0, Math.Min(359.92, sweep)));
        }

        private static Point PointOnCircle(Point center, double radius, double angleRadians)
        {
            return new Point(center.X + radius * Math.Cos(angleRadians),
                             center.Y + radius * Math.Sin(angleRadians));
        }

        private static Geometry CreateArc(Point center, double radius, double startAngleDegrees, double sweepDegrees)
        {
            double start = startAngleDegrees * Math.PI / 180.0;
            double end = (startAngleDegrees + sweepDegrees) * Math.PI / 180.0;
            Point startPoint = PointOnCircle(center, radius, start);
            Point endPoint = PointOnCircle(center, radius, end);

            PathFigure figure = new PathFigure();
            figure.StartPoint = startPoint;
            figure.IsClosed = false;
            figure.IsFilled = false;
            figure.Segments.Add(new ArcSegment(
                endPoint,
                new Size(radius, radius),
                0.0,
                sweepDegrees > 180.0,
                SweepDirection.Clockwise,
                true));

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            return geometry;
        }
    }
}
