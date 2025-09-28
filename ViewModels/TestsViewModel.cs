using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Linq;
using MMG.Models;
using MMG.Services;
using System.Windows;
using MMG.Views;

namespace MMG.ViewModels
{
    public class TestsViewModel : INotifyPropertyChanged
    {
        private readonly TestDatabaseService _testDatabaseService;
        private readonly TestExecutionService _testExecutionService;
        private readonly DatabaseService _databaseService;
        
        private ObservableCollection<TestScenario> _scenarios = new();
        private TestScenario? _selectedScenario;
        private ObservableCollection<TestStep> _currentSteps = new();
        private TestStep? _selectedStep;
        private ObservableCollection<SavedRequest> _savedRequests = new();
        
        private bool _isTestRunning = false;
        private string _testProgress = "";
        private int _progressPercentage = 0;
        private bool _isStopping = false;

        public TestsViewModel()
        {
            _databaseService = new DatabaseService();
            _testDatabaseService = new TestDatabaseService();
            
            // UdpClientService 인스턴스 생성 필요
            var udpClientService = new UdpClientService();
            _testExecutionService = new TestExecutionService(udpClientService, _databaseService, _testDatabaseService);

            // 이벤트 구독
            _testExecutionService.ProgressChanged += OnTestProgress;
            _testExecutionService.TestCompleted += OnTestCompleted;

            // Commands
            OpenCreateScenarioDialogCommand = new RelayCommand(() => OpenCreateScenarioDialog());
            DeleteScenarioCommand = new RelayCommand(async () => await DeleteScenario(), () => SelectedScenario != null && !IsTestRunning && !IsStopping);
            RunScenarioCommand = new RelayCommand(async () => await RunScenario(), () => SelectedScenario != null && !IsTestRunning && !IsStopping);
            StopTestCommand = new RelayCommand(async () => await StopTest(), () => IsTestRunning && !IsStopping);
            AddStepCommand = new RelayCommand(async () => await AddStep(), () => SelectedScenario != null && !IsTestRunning && !IsStopping);
            DeleteStepCommand = new RelayCommand(async () => await DeleteStep(), () => SelectedStep != null && !IsTestRunning && !IsStopping);
            SaveStepCommand = new RelayCommand(async () => await SaveStep(), () => SelectedStep != null && !IsTestRunning && !IsStopping);
            RefreshScenariosCommand = new RelayCommand(async () => await RefreshAll());

                        // Initial data loading
            _ = RefreshAll();
        }

        #region Properties

        public ObservableCollection<TestScenario> Scenarios
        {
            get => _scenarios;
            set
            {
                if (_scenarios != value)
                {
                    _scenarios = value;
                    OnPropertyChanged();
                }
            }
        }

        public TestScenario? SelectedScenario
        {
            get => _selectedScenario;
            set
            {
                if (_selectedScenario != value)
                {
                    _selectedScenario = value;
                    OnPropertyChanged();
                    _ = LoadSteps();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<TestStep> CurrentSteps
        {
            get => _currentSteps;
            set
            {
                if (_currentSteps != value)
                {
                    _currentSteps = value;
                    OnPropertyChanged();
                }
            }
        }

        public TestStep? SelectedStep
        {
            get => _selectedStep;
            set
            {
                if (_selectedStep != value)
                {
                    _selectedStep = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<SavedRequest> SavedRequests
        {
            get => _savedRequests;
            set
            {
                if (_savedRequests != value)
                {
                    _savedRequests = value;
                    OnPropertyChanged();
                }
            }
        }




        public bool IsTestRunning
        {
            get => _isTestRunning;
            set
            {
                if (_isTestRunning != value)
                {
                    _isTestRunning = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string TestProgress
        {
            get => _testProgress;
            set
            {
                if (_testProgress != value)
                {
                    _testProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ProgressPercentage
        {
            get => _progressPercentage;
            set
            {
                if (_progressPercentage != value)
                {
                    _progressPercentage = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsStopping
        {
            get => _isStopping;
            set
            {
                if (_isStopping != value)
                {
                    _isStopping = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand OpenCreateScenarioDialogCommand { get; }
        public ICommand DeleteScenarioCommand { get; }
        public ICommand RunScenarioCommand { get; }
        public ICommand StopTestCommand { get; }
        public ICommand AddStepCommand { get; }
        public ICommand DeleteStepCommand { get; }
        public ICommand SaveStepCommand { get; }
        public ICommand RefreshScenariosCommand { get; }

        #endregion

        #region Methods

        private async Task RefreshAll()
        {
            try
            {
                // 시나리오와 저장된 요청을 모두 새로고침
                await LoadScenarios();
                await LoadSavedRequests();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"데이터를 새로고침하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadScenarios()
        {
            try
            {
                var scenarios = await _testDatabaseService.GetAllScenariosAsync();
                Scenarios.Clear();
                foreach (var scenario in scenarios)
                {
                    Scenarios.Add(scenario);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"시나리오를 로드하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSavedRequests()
        {
            try
            {
                var requests = await _databaseService.GetAllRequestsAsync();
                SavedRequests.Clear();
                foreach (var request in requests)
                {
                    SavedRequests.Add(request);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"저장된 요청을 로드하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadSteps()
        {
            if (SelectedScenario == null) return;

            try
            {
                var steps = await _testDatabaseService.GetStepsForScenarioAsync(SelectedScenario.Id);
                CurrentSteps.Clear();
                foreach (var step in steps)
                {
                    CurrentSteps.Add(step);
                }
                
                // Command 상태 업데이트
                CommandManager.InvalidateRequerySuggested();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"스텝을 로드하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenCreateScenarioDialog()
        {
            var dialog = new CreateScenarioDialog();
            if (dialog.ShowDialog() == true)
            {
                _ = Task.Run(async () =>
                {
                    var newScenario = new TestScenario
                    {
                        Name = dialog.ScenarioName,
                        Description = ""
                    };

                    try
                    {
                        newScenario.Id = await _testDatabaseService.CreateScenarioAsync(newScenario);
                        
                        // UI 스레드에서 실행
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Scenarios.Add(newScenario);
                            SelectedScenario = newScenario; // 새로 생성된 시나리오를 자동으로 선택
                        });
                    }
                    catch (System.Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"시나리오를 생성하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        private async Task DeleteScenario()
        {
            if (SelectedScenario == null) return;

            var result = MessageBox.Show($"시나리오 '{SelectedScenario.Name}'을(를) 삭제하시겠습니까?", 
                "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _testDatabaseService.DeleteScenarioAsync(SelectedScenario.Id);
                    Scenarios.Remove(SelectedScenario);
                    SelectedScenario = null;
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"시나리오를 삭제하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task RunScenario()
        {
            if (SelectedScenario == null) return;

            IsTestRunning = true;
            IsStopping = false;
            SelectedScenario.IsRunning = true;
            TestProgress = "테스트를 시작하는 중...";
            ProgressPercentage = 0;

            try
            {
                // 실행 전에 최신 스텝 정보로 시나리오 업데이트
                var latestSteps = await _testDatabaseService.GetStepsForScenarioAsync(SelectedScenario.Id);
                SelectedScenario.Steps.Clear();
                foreach (var step in latestSteps)
                {
                    SelectedScenario.Steps.Add(step);
                }

                await _testExecutionService.RunScenarioAsync(SelectedScenario);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"테스트 실행 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                IsTestRunning = false;
                IsStopping = false;
                SelectedScenario.IsRunning = false;
            }
        }

        private async Task StopTest()
        {
            if (!IsTestRunning || IsStopping) return;

            IsStopping = true;
            TestProgress = "테스트를 중지하는 중...";
            
            _testExecutionService.StopTest();
            
            // 1초 대기 후 상태 초기화
            await Task.Delay(1000);
            
            IsTestRunning = false;
            IsStopping = false;
            if (SelectedScenario != null)
            {
                SelectedScenario.IsRunning = false;
            }
            TestProgress = "테스트가 중지되었습니다.";
        }

        private async Task AddStep()
        {
            if (SelectedScenario == null) return;

            var newStep = new TestStep
            {
                ScenarioId = SelectedScenario.Id,
                Name = $"Step {CurrentSteps.Count + 1}",
                Order = CurrentSteps.Count + 1,
                StepType = "SingleRequest"
            };

            try
            {
                newStep.Id = await _testDatabaseService.CreateStepAsync(newStep);
                CurrentSteps.Add(newStep);
                
                // 시나리오의 Steps 컬렉션에도 추가
                SelectedScenario.Steps.Add(newStep);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"스텝을 추가하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveStep()
        {
            if (SelectedStep == null) return;

            try
            {
                await _testDatabaseService.UpdateStepAsync(SelectedStep);
                MessageBox.Show("스텝이 성공적으로 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // 스텝 목록 새로고침으로 UI 업데이트
                await LoadSteps();
                
                // 선택된 시나리오의 Steps 컬렉션도 업데이트
                if (SelectedScenario != null)
                {
                    var latestSteps = await _testDatabaseService.GetStepsForScenarioAsync(SelectedScenario.Id);
                    SelectedScenario.Steps.Clear();
                    foreach (var step in latestSteps)
                    {
                        SelectedScenario.Steps.Add(step);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"스텝을 저장하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteStep()
        {
            if (SelectedStep == null) return;

            try
            {
                var stepToDelete = SelectedStep;
                await _testDatabaseService.DeleteStepAsync(stepToDelete.Id);
                
                // UI에서 제거
                CurrentSteps.Remove(stepToDelete);
                
                // 시나리오의 Steps 컬렉션에서도 제거
                if (SelectedScenario != null)
                {
                    var stepToRemove = SelectedScenario.Steps.FirstOrDefault(s => s.Id == stepToDelete.Id);
                    if (stepToRemove != null)
                    {
                        SelectedScenario.Steps.Remove(stepToRemove);
                    }
                }
                
                // 선택 해제
                SelectedStep = null;
                
                // Command의 CanExecute 상태 업데이트
                CommandManager.InvalidateRequerySuggested();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"스텝을 삭제하는 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnTestProgress(object? sender, TestProgressEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                TestProgress = e.Message;
                ProgressPercentage = (int)e.ProgressPercentage;
            });
        }

        private void OnTestCompleted(object? sender, TestCompletedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsTestRunning = false;
                IsStopping = false;
                if (SelectedScenario != null)
                {
                    SelectedScenario.IsRunning = false;
                }
                TestProgress = e.Summary;
                
                var message = $"테스트가 완료되었습니다.\n\n" +
                             $"총 스텝: {e.TotalSteps}\n" +
                             $"성공: {e.SuccessfulSteps}\n" +
                             $"실패: {e.FailedSteps}\n" +
                             $"실행 시간: {e.TotalExecutionTime.TotalSeconds:F1}초";

                MessageBox.Show(message, "테스트 결과", MessageBoxButton.OK, 
                    e.FailedSteps == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            });
        }

        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}