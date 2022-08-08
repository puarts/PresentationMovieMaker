
namespace PresentationMovieMaker.Views
{
    using System;
    using System.Linq;
    using System.Globalization;
    using System.Windows.Media;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.ComponentModel;

    public class StrokeAdorner : Adorner
    {
        private TextBlock _textBlock;

        private Brush _stroke;
        private ushort _strokeThickness;
        private double _strokeOpacity = 1.0;

        public Brush Stroke
        {
            get
            {
                return _stroke;
            }

            set
            {
                _stroke = value;
                _textBlock.InvalidateVisual();
                InvalidateVisual();
            }
        }

        public ushort StrokeThickness
        {
            get
            {
                return _strokeThickness;
            }

            set
            {
                _strokeThickness = value;
                _textBlock.InvalidateVisual();
                InvalidateVisual();
            }
        }



        public double StrokeOpacity
        {
            get
            {
                return _strokeOpacity;
            }

            set
            {
                _strokeOpacity = value;
                if (!Stroke.IsFrozen)
                {
                    Stroke.Opacity = StrokeOpacity;
                }
                _textBlock.InvalidateVisual();
                InvalidateVisual();
            }
        }

        public StrokeAdorner(UIElement adornedElement) : base(adornedElement)
        {
            _stroke = Brushes.Transparent;
            _textBlock = (TextBlock)adornedElement;
            ensureTextBlock();
            foreach (var property in TypeDescriptor.GetProperties(_textBlock).OfType<PropertyDescriptor>())
            {
                var dp = DependencyPropertyDescriptor.FromProperty(property);
                if (dp == null) continue;
                var metadata = dp.Metadata as FrameworkPropertyMetadata;
                if (metadata == null) continue;
                if (!metadata.AffectsRender) continue;
                dp.AddValueChanged(_textBlock, (s, e) => this.InvalidateVisual());
            }
        }

        private void ensureTextBlock()
        {
            if (_textBlock == null) throw new Exception("This adorner works on TextBlocks only");
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            ensureTextBlock();
            base.OnRender(drawingContext);
            var formattedText = new FormattedText(
                _textBlock.Text,
                CultureInfo.CurrentUICulture,
                _textBlock.FlowDirection,
                new Typeface(_textBlock.FontFamily, _textBlock.FontStyle, _textBlock.FontWeight, _textBlock.FontStretch),
                _textBlock.FontSize,
                    Brushes.Black // This brush does not matter since we use the geometry of the text. 
                    , 1.0
            );

            formattedText.TextAlignment = _textBlock.TextAlignment;
            formattedText.Trimming = _textBlock.TextTrimming;
            formattedText.LineHeight = _textBlock.LineHeight;
            formattedText.MaxTextWidth = _textBlock.ActualWidth - _textBlock.Padding.Left - _textBlock.Padding.Right;
            formattedText.MaxTextHeight = _textBlock.ActualHeight - _textBlock.Padding.Top;// - _textBlock.Padding.Bottom;
            while (formattedText.Extent == double.NegativeInfinity)
            {
                formattedText.MaxTextHeight++;
            }

            // Build the geometry object that represents the text.
            var _textGeometry = formattedText.BuildGeometry(new Point(_textBlock.Padding.Left, _textBlock.Padding.Top));
            var textPen = new Pen(Stroke, StrokeThickness);
            drawingContext.DrawGeometry(Brushes.Transparent, textPen, _textGeometry);
        }

    }


    public class StrokeTextBlock : TextBlock
    {
        private StrokeAdorner _adorner;
        private bool _adorned = false;

        public StrokeTextBlock()
        {
            _adorner = new StrokeAdorner(this);
            this.LayoutUpdated += StrokeTextBlock_LayoutUpdated;
        }

        private void StrokeTextBlock_LayoutUpdated(object? sender, EventArgs e)
        {
            if (_adorned) return;
            _adorned = true;
            var adornerLayer = AdornerLayer.GetAdornerLayer(this);
            adornerLayer.Add(_adorner);
            this.LayoutUpdated -= StrokeTextBlock_LayoutUpdated;
        }

        private static void strokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var stb = (StrokeTextBlock)d;
            stb._adorner.Stroke = (Brush)e.NewValue;
        }

        private static void strokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var stb = (StrokeTextBlock)d;
            stb._adorner.StrokeThickness = DependencyProperty.UnsetValue.Equals(e.NewValue) ? (ushort)0 : (ushort)e.NewValue;
        }

        private static void strokeOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var stb = (StrokeTextBlock)d;
            stb._adorner.StrokeOpacity = DependencyProperty.UnsetValue.Equals(e.NewValue) ? 1.0 : (double)e.NewValue;
        }

        /// <summary>
        /// Specifies the brush to use for the stroke and optional hightlight of the formatted text.
        /// </summary>
        public Brush Stroke
        {
            get
            {
                return (Brush)GetValue(StrokeProperty);
            }

            set
            {
                SetValue(StrokeProperty, value);
            }
        }

        /// <summary>
        /// Identifies the Stroke dependency property.
        /// </summary>
        public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
            "Stroke",
            typeof(Brush),
            typeof(StrokeTextBlock),
            new FrameworkPropertyMetadata(
                    new SolidColorBrush(Colors.Teal),
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    new PropertyChangedCallback(strokeChanged),
                    null
                    )
            );

        /// <summary>
        ///     The stroke thickness of the font.
        /// </summary>
        public ushort StrokeThickness
        {
            get
            {
                return (ushort)GetValue(StrokeThicknessProperty);
            }

            set
            {
                SetValue(StrokeThicknessProperty, value);
            }
        }

        /// <summary>
        /// Identifies the StrokeThickness dependency property.
        /// </summary>
        public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
            "StrokeThickness",
            typeof(ushort),
            typeof(StrokeTextBlock),
            new FrameworkPropertyMetadata(
                    (ushort)0,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    new PropertyChangedCallback(strokeOpacityChanged),
                    null
                    )
            );


        /// <summary>
        /// The stroke opacity of the font.
        /// </summary>
        public ushort StrokeOpacity
        {
            get
            {
                return (ushort)GetValue(StrokeOpacityProperty);
            }

            set
            {
                SetValue(StrokeOpacityProperty, value);
            }
        }

        /// <summary>
        /// Identifies the StrokeThickness dependency property.
        /// </summary>
        public static readonly DependencyProperty StrokeOpacityProperty = DependencyProperty.Register(
            nameof(StrokeOpacity),
            typeof(ushort),
            typeof(StrokeTextBlock),
            new FrameworkPropertyMetadata(
                    (double)1.0,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    new PropertyChangedCallback(strokeThicknessChanged),
                    null
                    )
            );
    }

    public static class Adorning
    {
        public static Brush GetStroke(DependencyObject obj)
        {
            return (Brush)obj.GetValue(StrokeProperty);
        }
        public static void SetStroke(DependencyObject obj, Brush value)
        {
            obj.SetValue(StrokeProperty, value);
        }
        // Using a DependencyProperty as the backing store for Stroke. This enables animation, styling, binding, etc...  
        public static readonly DependencyProperty StrokeProperty =
        DependencyProperty.RegisterAttached("Stroke", typeof(Brush), typeof(Adorning), new PropertyMetadata(Brushes.Transparent, strokeChanged));

        private static void strokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var stroke = (Brush)e.NewValue;
            ensureAdorner(d, a => a.Stroke = stroke);
        }

        private static void ensureAdorner(DependencyObject d, Action<StrokeAdorner> action)
        {
            var tb = d as TextBlock;
            if (tb == null) throw new Exception("StrokeAdorner only works on TextBlocks");
            EventHandler? f = null;
            f = new EventHandler((o, e) =>
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(tb);
                if (adornerLayer == null) throw new Exception("AdornerLayer should not be empty");
                var adorners = adornerLayer.GetAdorners(tb);
                var adorner = adorners == null ? null : adorners.OfType<StrokeAdorner>().FirstOrDefault();
                if (adorner == null)
                {
                    adorner = new StrokeAdorner(tb);
                    adornerLayer.Add(adorner);
                }
                tb.LayoutUpdated -= f;
                action(adorner);
            });
            tb.LayoutUpdated += f;
        }

        public static double GetStrokeThickness(DependencyObject obj)
        {
            return (double)obj.GetValue(StrokeThicknessProperty);
        }
        public static void SetStrokeThickness(DependencyObject obj, double value)
        {
            obj.SetValue(StrokeThicknessProperty, value);
        }
        // Using a DependencyProperty as the backing store for StrokeThickness. This enables animation, styling, binding, etc...  
        public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.RegisterAttached("StrokeThickness", typeof(double), typeof(Adorning), new PropertyMetadata(0.0, strokeThicknessChanged));

        private static void strokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ensureAdorner(d, a =>
            {
                if (DependencyProperty.UnsetValue.Equals(e.NewValue)) return;
                a.StrokeThickness = (ushort)(double)e.NewValue;
            });
        }


        public static double GetStrokeOpacity(DependencyObject obj)
        {
            return (double)obj.GetValue(StrokeOpacityProperty);
        }
        public static void SetStrokeOpacity(DependencyObject obj, double value)
        {
            obj.SetValue(StrokeOpacityProperty, value);
        }
        // Using a DependencyProperty as the backing store for StrokeOpacity. This enables animation, styling, binding, etc...  
        public static readonly DependencyProperty StrokeOpacityProperty =
        DependencyProperty.RegisterAttached("StrokeOpacity", typeof(double), typeof(Adorning), new PropertyMetadata(0.0, strokeOpacityChanged));

        private static void strokeOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ensureAdorner(d, a =>
            {
                if (DependencyProperty.UnsetValue.Equals(e.NewValue)) return;
                a.StrokeOpacity = (double)e.NewValue;
            });
        }
    }
}
