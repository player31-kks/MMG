using System.Threading.Tasks;
using MMG.Models;
using MMG.ViewModels.Spec;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MMG.ViewModels
{
    public partial class NavigationViewModel : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentContent))]
        private string selectedTab = "API";

        [ObservableProperty]
        private object? currentContent;

        private readonly MainViewModel _mainViewModel;
        private readonly TestsViewModel _testsViewModel;
        private readonly SpecViewModel _specViewModel;

        public NavigationViewModel()
        {
            _mainViewModel = new MainViewModel();
            _testsViewModel = new TestsViewModel();
            _specViewModel = new SpecViewModel();

            // SpecViewModel 이벤트 연결
            _specViewModel.CreateApiRequestRequested += OnCreateApiRequestRequested;

            // 기본값으로 API 탭 선택
            CurrentContent = _mainViewModel;
        }

        partial void OnSelectedTabChanged(string value)
        {
            UpdateCurrentContent();
        }

        #region Commands

        [RelayCommand]
        private void ApiTab() => SelectedTab = "API";

        [RelayCommand]
        private void TestsTab() => SelectedTab = "Tests";

        [RelayCommand]
        private void SpecTab() => SelectedTab = "Spec";

        #endregion

        #region Private Methods

        private void UpdateCurrentContent()
        {
            CurrentContent = SelectedTab switch
            {
                "API" => _mainViewModel,
                "Tests" => _testsViewModel,
                "Spec" => _specViewModel,
                _ => _mainViewModel
            };

            // Tests 탭으로 전환할 때 자동으로 새로고침
            if (SelectedTab == "Tests")
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

        private void OnCreateApiRequestRequested(object? sender, CreateApiRequestEventArgs args)
        {
            // SavedRequestsViewModel을 통해 저장 다이얼로그 표시 및 저장
            _ = _mainViewModel.SavedRequestsViewModel.SaveFromSpec(args);

            // API 탭으로 전환
            SelectedTab = "API";
        }

        #endregion
    }
}