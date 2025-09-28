using System.ComponentModel;
using System.Windows.Input;

namespace MMG.ViewModels
{
    public class NavigationViewModel : INotifyPropertyChanged
    {
        private string _selectedTab = "API";
        private object? _currentContent;
        private readonly MainViewModel _mainViewModel;
        private readonly TestsViewModel _testsViewModel;

        public NavigationViewModel()
        {
            _mainViewModel = new MainViewModel();
            _testsViewModel = new TestsViewModel();
            
            ApiTabCommand = new RelayCommand(() => SelectedTab = "API");
            TestsTabCommand = new RelayCommand(() => SelectedTab = "Tests");
            
            // 기본값으로 API 탭 선택
            CurrentContent = _mainViewModel;
        }

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != value)
                {
                    _selectedTab = value;
                    OnPropertyChanged();
                    UpdateCurrentContent();
                }
            }
        }

        public object? CurrentContent
        {
            get => _currentContent;
            set
            {
                if (_currentContent != value)
                {
                    _currentContent = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ApiTabCommand { get; }
        public ICommand TestsTabCommand { get; }

        private void UpdateCurrentContent()
        {
            CurrentContent = _selectedTab switch
            {
                "API" => _mainViewModel,
                "Tests" => _testsViewModel,
                _ => _mainViewModel
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}