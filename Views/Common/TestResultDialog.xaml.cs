using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MMG.Views.Common
{
    public partial class TestResultDialog : Window
    {
        public TestResultDialog(int totalSteps, int successSteps, int failedSteps, TimeSpan executionTime)
        {
            InitializeComponent();

            SetupStats(totalSteps, successSteps, failedSteps, executionTime);
            SetupAppearance(failedSteps > 0);
        }

        private void SetupStats(int totalSteps, int successSteps, int failedSteps, TimeSpan executionTime)
        {
            TotalStepsText.Text = totalSteps.ToString();
            SuccessStepsText.Text = successSteps.ToString();
            FailedStepsText.Text = failedSteps.ToString();
            ExecutionTimeText.Text = $"{executionTime.TotalSeconds:F1}초";
        }

        private void SetupAppearance(bool hasFailed)
        {
            if (hasFailed)
            {
                // Error state
                HeaderBorder.Background = new SolidColorBrush(Color.FromRgb(254, 242, 242)); // red-50
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(254, 202, 202)); // red-200
                IconText.Text = "✕";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // red-500
                IconText.FontWeight = FontWeights.Bold;
                TitleText.Text = "테스트 실패";
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28)); // red-700
                SubtitleText.Text = "일부 스텝에서 오류가 발생했습니다";
            }
            else
            {
                // Success state
                HeaderBorder.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245)); // green-50
                IconBorder.Background = new SolidColorBrush(Color.FromRgb(167, 243, 208)); // green-200
                IconText.Text = "✓";
                IconText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // green-500
                IconText.FontWeight = FontWeights.Bold;
                TitleText.Text = "테스트 성공";
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87)); // green-700
                SubtitleText.Text = "모든 스텝이 성공적으로 실행되었습니다";
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Static helper method
        public static void Show(int totalSteps, int successSteps, int failedSteps, TimeSpan executionTime, Window? owner = null)
        {
            var dialog = new TestResultDialog(totalSteps, successSteps, failedSteps, executionTime);
            if (owner != null)
                dialog.Owner = owner;
            dialog.ShowDialog();
        }
    }
}
