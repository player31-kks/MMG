using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MMG.Views.Common
{
    public enum MessageDialogType
    {
        Success,
        Error,
        Warning,
        Information
    }

    public enum MessageDialogButtons
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel
    }

    public partial class ModernMessageDialog : Window
    {
        public new bool? DialogResult { get; private set; }
        public string? SelectedButton { get; private set; }

        public ModernMessageDialog(string title, string message, MessageDialogType type = MessageDialogType.Information, MessageDialogButtons buttons = MessageDialogButtons.OK)
        {
            InitializeComponent();

            TitleText.Text = title;
            MessageText.Text = message;

            SetupIcon(type);
            SetupButtons(buttons);
        }

        private void SetupIcon(MessageDialogType type)
        {
            switch (type)
            {
                case MessageDialogType.Success:
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(209, 250, 229)); // green-100
                    IconText.Text = "✓";
                    IconText.Foreground = (Brush)FindResource("SuccessIconColor");
                    IconText.FontWeight = FontWeights.Bold;
                    break;
                case MessageDialogType.Error:
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226)); // red-100
                    IconText.Text = "✕";
                    IconText.Foreground = (Brush)FindResource("ErrorIconColor");
                    IconText.FontWeight = FontWeights.Bold;
                    break;
                case MessageDialogType.Warning:
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199)); // yellow-100
                    IconText.Text = "⚠";
                    IconText.Foreground = (Brush)FindResource("WarningIconColor");
                    break;
                case MessageDialogType.Information:
                default:
                    IconBorder.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)); // blue-100
                    IconText.Text = "ℹ";
                    IconText.Foreground = (Brush)FindResource("InfoIconColor");
                    break;
            }
        }

        private void SetupButtons(MessageDialogButtons buttonType)
        {
            ButtonPanel.Children.Clear();

            switch (buttonType)
            {
                case MessageDialogButtons.OK:
                    AddButton("확인", true, true);
                    break;
                case MessageDialogButtons.OKCancel:
                    AddButton("취소", false, false);
                    AddButton("확인", true, true);
                    break;
                case MessageDialogButtons.YesNo:
                    AddButton("아니오", false, false);
                    AddButton("예", true, true);
                    break;
                case MessageDialogButtons.YesNoCancel:
                    AddButton("취소", null, false);
                    AddButton("아니오", false, false);
                    AddButton("예", true, true);
                    break;
            }
        }

        private void AddButton(string text, bool? result, bool isPrimary)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 80,
                Height = 36,
                FontSize = 14,
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0)
            };

            if (isPrimary)
            {
                button.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)); // blue-500
                button.Foreground = Brushes.White;
                button.IsDefault = true;
            }
            else
            {
                button.Background = Brushes.White;
                button.Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)); // gray-700
            }

            button.Template = CreateButtonTemplate(isPrimary);
            button.Click += (s, e) =>
            {
                DialogResult = result;
                SelectedButton = text;
                Close();
            };

            ButtonPanel.Children.Add(button);
        }

        private ControlTemplate CreateButtonTemplate(bool isPrimary)
        {
            var template = new ControlTemplate(typeof(Button));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, isPrimary
                ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
                : Brushes.White);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(Border.BorderBrushProperty, isPrimary
                ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
                : new SolidColorBrush(Color.FromRgb(209, 213, 219))); // gray-300
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(16, 0, 16, 0));
            borderFactory.Name = "border";

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenterFactory);
            template.VisualTree = borderFactory;

            // Triggers for hover effects
            var mouseOverTrigger = new Trigger
            {
                Property = Button.IsMouseOverProperty,
                Value = true
            };
            mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                isPrimary
                    ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) // blue-600
                    : new SolidColorBrush(Color.FromRgb(249, 250, 251)), // gray-50
                "border"));
            template.Triggers.Add(mouseOverTrigger);

            var pressedTrigger = new Trigger
            {
                Property = Button.IsPressedProperty,
                Value = true
            };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty,
                isPrimary
                    ? new SolidColorBrush(Color.FromRgb(29, 78, 216)) // blue-700
                    : new SolidColorBrush(Color.FromRgb(243, 244, 246)), // gray-100
                "border"));
            template.Triggers.Add(pressedTrigger);

            return template;
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
            DialogResult = null;
            Close();
        }

        // Static helper methods for easy usage
        public static void Show(string message, string title = "알림", MessageDialogType type = MessageDialogType.Information, Window? owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, type, MessageDialogButtons.OK);
            if (owner != null)
                dialog.Owner = owner;
            dialog.ShowDialog();
        }

        public static void ShowSuccess(string message, string title = "성공", Window? owner = null)
        {
            Show(message, title, MessageDialogType.Success, owner);
        }

        public static void ShowError(string message, string title = "오류", Window? owner = null)
        {
            Show(message, title, MessageDialogType.Error, owner);
        }

        public static void ShowWarning(string message, string title = "경고", Window? owner = null)
        {
            Show(message, title, MessageDialogType.Warning, owner);
        }

        public static void ShowInfo(string message, string title = "알림", Window? owner = null)
        {
            Show(message, title, MessageDialogType.Information, owner);
        }

        public static bool? ShowConfirm(string message, string title = "확인", Window? owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, MessageDialogType.Information, MessageDialogButtons.YesNo);
            if (owner != null)
                dialog.Owner = owner;
            dialog.ShowDialog();
            return dialog.DialogResult;
        }

        public static bool? ShowOKCancel(string message, string title = "확인", MessageDialogType type = MessageDialogType.Information, Window? owner = null)
        {
            var dialog = new ModernMessageDialog(title, message, type, MessageDialogButtons.OKCancel);
            if (owner != null)
                dialog.Owner = owner;
            dialog.ShowDialog();
            return dialog.DialogResult;
        }
    }
}
