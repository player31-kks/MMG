using System.ComponentModel;
using System.Windows.Input;
using System.Threading.Tasks;
using MMG.ViewModels.Spec;

namespace MMG.ViewModels
{
    public class NavigationViewModel : INotifyPropertyChanged
    {
        private string _selectedTab = "API";
        private object? _currentContent;
        private readonly MainViewModel _mainViewModel;
        private readonly TestsViewModel _testsViewModel;
        private readonly SpecViewModel _specViewModel;

        public NavigationViewModel()
        {
            _mainViewModel = new MainViewModel();
            _testsViewModel = new TestsViewModel();
            _specViewModel = new SpecViewModel();

            ApiTabCommand = new RelayCommand(() => SelectedTab = "API");
            TestsTabCommand = new RelayCommand(() => SelectedTab = "Tests");
            SpecTabCommand = new RelayCommand(() => SelectedTab = "Spec");

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
        public ICommand SpecTabCommand { get; }

        private void UpdateCurrentContent()
        {
            CurrentContent = _selectedTab switch
            {
                "API" => _mainViewModel,
                "Tests" => _testsViewModel,
                "Spec" => _specViewModel,
                _ => _mainViewModel
            };

            // Tests 탭으로 전환할 때 자동으로 새로고침
            if (_selectedTab == "Tests")
            {
                RefreshTestsData();
            }
        }

        private void RefreshTestsData()
        {
            try
            {
                // TestsViewModel의 RefreshAll 메서드 호출
                if (_testsViewModel.RefreshScenariosCommand.CanExecute(null))
                {
                    _testsViewModel.RefreshScenariosCommand.Execute(null);
                }
            }
            catch (System.Exception ex)
            {
                // 오류 처리 (필요시)
                System.Diagnostics.Debug.WriteLine($"Tests 데이터 새로고침 중 오류: {ex.Message}");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}