using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MMG.Models;

namespace MMG.Views.Test
{
    public partial class TestLogWindow : Window
    {
        private ObservableCollection<TestLogItem> _logItems;

        public TestLogWindow(ObservableCollection<TestLogItem> logItems)
        {
            InitializeComponent();
            _logItems = logItems;
            LogListBox.ItemsSource = _logItems;

            // 컬렉션 변경 이벤트 구독
            _logItems.CollectionChanged += LogItems_CollectionChanged;

            UpdateUI();
        }

        private void LogItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateUI();

                // 자동 스크롤
                if (AutoScrollCheckBox.IsChecked == true && LogListBox.Items.Count > 0)
                {
                    LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
                }
            });
        }

        private void UpdateUI()
        {
            LogCountText.Text = $"({_logItems.Count}개)";
            EmptyStateText.Visibility = _logItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // 마지막 로그 항목에 따라 상태 표시 업데이트
            if (_logItems.Count > 0)
            {
                var lastLog = _logItems[_logItems.Count - 1];
                StatusText.Text = lastLog.Message;

                switch (lastLog.Level)
                {
                    case LogLevel.Error:
                        StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DC3545"));
                        break;
                    case LogLevel.Warning:
                        StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFC107"));
                        break;
                    case LogLevel.Success:
                        StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#28A745"));
                        break;
                    default:
                        StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#17A2B8"));
                        break;
                }
            }
            else
            {
                StatusText.Text = "대기 중";
                StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6C757D"));
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _logItems.Clear();
            UpdateUI();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 이벤트 구독 해제
            _logItems.CollectionChanged -= LogItems_CollectionChanged;
            base.OnClosed(e);
        }
    }
}
