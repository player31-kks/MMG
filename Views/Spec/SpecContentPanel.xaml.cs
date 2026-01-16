using System.Windows.Controls;
using MMG.ViewModels.Spec;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace MMG.Views.Spec
{
    /// <summary>
    /// SpecContentPanel.xaml의 코드 비하인드
    /// </summary>
    public partial class SpecContentPanel : UserControl
    {
        private SpecViewModel? _viewModel;
        private DocViewerViewModel? _docViewModel;

        public SpecContentPanel()
        {
            InitializeComponent();

            // DataContext가 변경될 때 (NavigationViewModel에서 전달) 이벤트 연결
            DataContextChanged += OnDataContextChanged;

            // 문서 뷰 라디오 버튼 체크 이벤트 연결
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // DocViewRadio 버튼에 이벤트 연결
            if (DocViewRadio != null)
            {
                DocViewRadio.Checked += OnDocViewRadioChecked;
            }
        }

        private void OnDocViewRadioChecked(object sender, System.Windows.RoutedEventArgs e)
        {
            // 문서 뷰로 전환 시 자동으로 스펙 적용
            if (_viewModel != null && !string.IsNullOrEmpty(_viewModel.SpecContent))
            {
                if (_viewModel.RefreshFromContentCommand.CanExecute(null))
                {
                    _viewModel.RefreshFromContentCommand.Execute(null);
                }
            }
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            // 이전 ViewModel 이벤트 해제
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            // 새 ViewModel 연결
            if (e.NewValue is SpecViewModel specViewModel)
            {
                _viewModel = specViewModel;

                // DocViewerViewModel은 SpecViewModel에서 관리하도록 변경
                if (_docViewModel == null)
                {
                    _docViewModel = Ioc.Default.GetService<DocViewerViewModel>();
                    _docViewModel.PropertyChanged += OnDocViewModelPropertyChanged;
                }

                _viewModel.PropertyChanged += OnViewModelPropertyChanged;

                // 현재 스펙이 있으면 문서 뷰어 업데이트
                if (_viewModel.CurrentSpec != null)
                {
                    _docViewModel.LoadSpec(_viewModel.CurrentSpec);
                    UpdateDocBrowser();
                }
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SpecViewModel.CurrentSpec) && _viewModel?.CurrentSpec != null)
            {
                _docViewModel?.LoadSpec(_viewModel.CurrentSpec);
                UpdateDocBrowser();
            }
        }

        private void OnDocViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DocViewerViewModel.HtmlContent))
            {
                UpdateDocBrowser();
            }
        }

        private void UpdateDocBrowser()
        {
            try
            {
                if (_docViewModel != null && !string.IsNullOrEmpty(_docViewModel.HtmlContent))
                {
                    DocBrowser.NavigateToString(_docViewModel.HtmlContent);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebBrowser 업데이트 오류: {ex.Message}");
            }
        }
    }
}
