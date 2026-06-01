using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MMG.Views.Common
{
    public partial class WarningIcon : UserControl
    {
        public static readonly DependencyProperty TriangleFillProperty = DependencyProperty.Register(
            nameof(TriangleFill),
            typeof(Brush),
            typeof(WarningIcon),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"))));

        public static readonly DependencyProperty SymbolFillProperty = DependencyProperty.Register(
            nameof(SymbolFill),
            typeof(Brush),
            typeof(WarningIcon),
            new PropertyMetadata(Brushes.White));

        public Brush TriangleFill
        {
            get => (Brush)GetValue(TriangleFillProperty);
            set => SetValue(TriangleFillProperty, value);
        }

        public Brush SymbolFill
        {
            get => (Brush)GetValue(SymbolFillProperty);
            set => SetValue(SymbolFillProperty, value);
        }

        public WarningIcon()
        {
            InitializeComponent();
        }
    }
}