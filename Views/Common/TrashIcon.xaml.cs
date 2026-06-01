using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MMG.Views.Common
{
    public partial class TrashIcon : UserControl
    {
        public static readonly DependencyProperty IconFillProperty = DependencyProperty.Register(
            nameof(IconFill),
            typeof(Brush),
            typeof(TrashIcon),
            new PropertyMetadata(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"))));

        public Brush IconFill
        {
            get => (Brush)GetValue(IconFillProperty);
            set => SetValue(IconFillProperty, value);
        }

        public TrashIcon()
        {
            InitializeComponent();
        }
    }
}