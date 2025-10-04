using System.Windows;
using System.Windows.Controls;
using MMG.ViewModels;
using MMG.Models;

namespace MMG.Views.Test
{
    public partial class TestsContentPanel : UserControl
    {
        public TestsContentPanel()
        {
            InitializeComponent();
        }

        private void SetFrequency_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                var frequency = button.Tag.ToString();

                // DataContext를 통해 TestsViewModel에 접근
                if (DataContext is TestsViewModel viewModel && viewModel.SelectedStep != null)
                {
                    if (double.TryParse(frequency, out double freq))
                    {
                        viewModel.SelectedStep.FrequencyHz = freq;
                    }
                }
            }
        }

        private void SetDuration_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                var duration = button.Tag.ToString();

                // DataContext를 통해 TestsViewModel에 접근
                if (DataContext is TestsViewModel viewModel && viewModel.SelectedStep != null)
                {
                    if (int.TryParse(duration, out int dur))
                    {
                        viewModel.SelectedStep.DurationSeconds = dur;
                    }
                }
            }
        }
    }
}