using System.Collections.ObjectModel;
using MMG.Models;
using MMG.Services;
using System.Windows;
using MMG.Views.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MMG.ViewModels
{
    public partial class TestsViewModel : ObservableObject
    {
        private readonly TestDatabaseService _testDatabaseService;
        private readonly TestExecutionService _testExecutionService;
        private readonly DatabaseService _databaseService;

        [ObservableProperty]
        private ObservableCollection<TestScenario> scenarios = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunScenarioCommand), nameof(AddStepCommand), nameof(AddTestStepCommand), nameof(RunSelectedScenarioCommand))]
        private TestScenario? selectedScenario;

        [ObservableProperty]
        private ObservableCollection<TestStep> currentSteps = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteStepCommand), nameof(SaveStepCommand))]
        private TestStep? selectedStep;

        [ObservableProperty]
        private ObservableCollection<SavedRequest> savedRequests = new();

        [ObservableProperty]
        private ObservableCollection<ReceivedDataItem> receivedDataItems = new();

        [ObservableProperty]
        private ObservableCollection<TestLogItem> logItems = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteScenarioCommand), nameof(RunScenarioCommand), nameof(StopTestCommand), nameof(AddStepCommand), nameof(DeleteStepCommand), nameof(SaveStepCommand), nameof(AddTestStepCommand), nameof(RunSelectedScenarioCommand), nameof(RunTestStepCommand), nameof(DeleteTestStepCommand))]
        private bool isTestRunning = false;

        [ObservableProperty]
        private string testProgress = "";

        [ObservableProperty]
        private int progressPercentage = 0;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeleteScenarioCommand), nameof(RunScenarioCommand), nameof(StopTestCommand), nameof(AddStepCommand), nameof(DeleteStepCommand), nameof(SaveStepCommand), nameof(AddTestStepCommand), nameof(RunSelectedScenarioCommand), nameof(RunTestStepCommand), nameof(DeleteTestStepCommand))]
        private bool isStopping = false;

        [ObservableProperty]
        private bool autoScrollLog = true;

        public TestsViewModel(DatabaseService databaseService, TestDatabaseService testDatabaseService, TestExecutionService testExecutionService)
        {
            _databaseService = databaseService;
            _testDatabaseService = testDatabaseService;
            _testExecutionService = testExecutionService;

            // 이벤트 구독
            _testExecutionService.ProgressChanged += OnTestProgress;
            _testExecutionService.TestCompleted += OnTestCompleted;
            _testExecutionService.DataReceived += OnDataReceived;
            _testExecutionService.LogAdded += OnLogAdded;

            // Initial data loading
            _ = RefreshAll();
        }

        partial void OnSelectedScenarioChanged(TestScenario? value)
        {
            _ = LoadSteps();
        }

        #region Properties

        // 테스트 결과 관련 속성들
        public int TotalScenarios => Scenarios?.Count ?? 0;
        public int SuccessfulTests { get; private set; }
        public int FailedTests { get; private set; }
        public double SuccessRate => TotalScenarios > 0 ? (double)SuccessfulTests / TotalScenarios * 100 : 0;
        public ObservableCollection<TestResult> RecentResults { get; } = new ObservableCollection<TestResult>();
        public DateTime LastUpdateTime { get; private set; } = DateTime.Now;

        #endregion

        #region Commands

        [RelayCommand]
        private void OpenCreateScenarioDialog() => OpenCreateScenarioDialogInternal();

        [RelayCommand(CanExecute = nameof(CanDeleteScenario))]
        private async Task DeleteScenario(TestScenario? scenario)
        {
            if (scenario != null)
                await DeleteScenarioInternal(scenario);
        }

        private bool CanDeleteScenario(TestScenario? scenario) => scenario != null && !IsTestRunning && !IsStopping;

        [RelayCommand(CanExecute = nameof(CanRunScenario))]
        private async Task RunScenario() => await RunScenarioInternal();

        private bool CanRunScenario() => SelectedScenario != null && !IsTestRunning && !IsStopping;

        [RelayCommand(CanExecute = nameof(CanStopTest))]
        private async Task StopTest() => await StopTestInternal();

        private bool CanStopTest() => IsTestRunning && !IsStopping;

        [RelayCommand(CanExecute = nameof(CanAddStep))]
        private async Task AddStep() => await AddStepInternal();

        private bool CanAddStep() => SelectedScenario != null && !IsTestRunning && !IsStopping;

        [RelayCommand(CanExecute = nameof(CanDeleteStep))]
        private async Task DeleteStep() => await DeleteStepInternal();

        private bool CanDeleteStep() => SelectedStep != null && !IsTestRunning && !IsStopping;

        [RelayCommand(CanExecute = nameof(CanSaveStep))]
        private async Task SaveStep() => await SaveStepInternal();

        private bool CanSaveStep() => SelectedScenario != null && SelectedScenario.Steps.Count > 0 && !IsTestRunning && !IsStopping;

        [RelayCommand]
        private async Task RefreshScenarios() => await RefreshAll();

        [RelayCommand]
        private void RenameScenario(TestScenario? scenario)
        {
            if (scenario != null)
                StartRenaming(scenario);
        }

        [RelayCommand]
        private async Task SaveScenarioRename(TestScenario? scenario)
        {
            if (scenario != null)
                await SaveRename(scenario);
        }

        [RelayCommand]
        private void CancelScenarioRename(TestScenario? scenario)
        {
            if (scenario != null)
                CancelRename(scenario);
        }

        [RelayCommand(CanExecute = nameof(CanAddStep))]
        private async Task AddTestStep() => await AddStepInternal();

        [RelayCommand(CanExecute = nameof(CanRunScenario))]
        private async Task RunSelectedScenario() => await RunScenarioInternal();

        [RelayCommand(CanExecute = nameof(CanRunTestStep))]
        private async Task RunTestStep(TestStep? step)
        {
            if (step != null)
                await RunSingleStep(step);
        }

        private bool CanRunTestStep(TestStep? step) => step != null && !IsTestRunning && !IsStopping;

        [RelayCommand(CanExecute = nameof(CanDeleteTestStep))]
        private async Task DeleteTestStep(TestStep? step)
        {
            if (step != null)
                await DeleteSingleStep(step);
        }

        private bool CanDeleteTestStep(TestStep? step) => step != null && !IsTestRunning && !IsStopping;

        [RelayCommand]
        private void OpenDetailedResults() => OpenDetailedResultsInternal();

        [RelayCommand]
        private async Task RefreshResults() => await RefreshResultsInternal();

        [RelayCommand]
        private void SelectStep(TestStep? step)
        {
            if (step != null)
                SelectedStep = step;
        }

        [RelayCommand]
        private void ClearLog() => ClearLogInternal();

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
                ModernMessageDialog.ShowError($"데이터를 새로고침하는 중 오류가 발생했습니다: {ex.Message}");
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
                ModernMessageDialog.ShowError($"시나리오를 로드하는 중 오류가 발생했습니다: {ex.Message}");
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
                ModernMessageDialog.ShowError($"저장된 요청을 로드하는 중 오류가 발생했습니다: {ex.Message}");
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
            }
            catch (System.Exception ex)
            {
                ModernMessageDialog.ShowError($"스텝을 로드하는 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void OpenCreateScenarioDialogInternal()
        {
            var dialog = new CreateScenarioDialog();
            if (dialog.ShowDialog() == true)
            {
                _ = Task.Run(async () =>
                {
                    var newScenario = new TestScenario
                    {
                        Name = dialog.ScenarioName,
                        Description = "",
                        UseBindPort = dialog.UseBindPort,
                        BindPort = dialog.BindPort
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
                            ModernMessageDialog.ShowError($"시나리오를 생성하는 중 오류가 발생했습니다: {ex.Message}");
                        });
                    }
                });
            }
        }

        private async Task DeleteScenarioInternal(TestScenario scenario)
        {
            if (scenario == null) return;

            var confirmed = ConfirmDialog.Show("시나리오 삭제", $"시나리오 '{scenario.Name}'을(를) 삭제하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.");

            if (confirmed)
            {
                try
                {
                    await _testDatabaseService.DeleteScenarioAsync(scenario.Id);
                    Scenarios.Remove(scenario);

                    // 삭제된 시나리오가 현재 선택된 시나리오라면 선택 해제
                    if (SelectedScenario == scenario)
                    {
                        SelectedScenario = null;
                    }
                }
                catch (System.Exception ex)
                {
                    ModernMessageDialog.ShowError($"시나리오를 삭제하는 중 오류가 발생했습니다: {ex.Message}");
                }
            }
        }

        private async Task RunScenarioInternal()
        {
            if (SelectedScenario == null) return;

            IsTestRunning = true;
            IsStopping = false;
            SelectedScenario.IsRunning = true;
            TestProgress = "테스트를 시작하는 중...";
            ProgressPercentage = 0;

            // 수신 데이터 클리어
            ReceivedDataItems.Clear();

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
                ModernMessageDialog.ShowError($"테스트 실행 중 오류가 발생했습니다: {ex.Message}");
                IsTestRunning = false;
                IsStopping = false;
                SelectedScenario.IsRunning = false;
            }
        }

        private async Task StopTestInternal()
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

        private async Task AddStepInternal()
        {
            if (SelectedScenario == null) return;

            var newStep = new TestStep
            {
                ScenarioId = SelectedScenario.Id,
                Name = $"Step {CurrentSteps.Count + 1}",
                Order = CurrentSteps.Count + 1,
                StepType = "Immediate"
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
                ModernMessageDialog.ShowError($"스텝을 추가하는 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private async Task SaveStepInternal()
        {
            if (SelectedScenario == null) return;

            try
            {
                // 현재 시나리오의 모든 스텝 저장
                int savedCount = 0;
                foreach (var step in SelectedScenario.Steps)
                {
                    await _testDatabaseService.UpdateStepAsync(step);
                    savedCount++;
                }

                ModernMessageDialog.ShowSuccess($"{savedCount}개의 스텝이 성공적으로 저장되었습니다.", "저장 완료");

                // 스텝 목록 새로고침으로 UI 업데이트
                await LoadSteps();

                // 선택된 시나리오의 Steps 컬렉션도 업데이트
                var latestSteps = await _testDatabaseService.GetStepsForScenarioAsync(SelectedScenario.Id);
                SelectedScenario.Steps.Clear();
                foreach (var step in latestSteps)
                {
                    SelectedScenario.Steps.Add(step);
                }
            }
            catch (System.Exception ex)
            {
                ModernMessageDialog.ShowError($"스텝을 저장하는 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private async Task DeleteStepInternal()
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
            }
            catch (System.Exception ex)
            {
                ModernMessageDialog.ShowError($"스텝을 삭제하는 중 오류가 발생했습니다: {ex.Message}");
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

                // 모던 테스트 결과 다이얼로그 표시
                TestResultDialog.Show(e.TotalSteps, e.SuccessfulSteps, e.FailedSteps, e.TotalExecutionTime);
            });
        }

        private void StartRenaming(TestScenario? scenario)
        {
            if (scenario == null) return;

            // 다른 편집 중인 시나리오들 종료
            foreach (var s in Scenarios)
            {
                if (s.IsEditing)
                {
                    s.IsEditing = false;
                }
            }

            scenario.IsEditing = true;
        }

        private async Task SaveRename(TestScenario? scenario)
        {
            if (scenario == null || string.IsNullOrWhiteSpace(scenario.Name))
            {
                if (scenario != null && string.IsNullOrWhiteSpace(scenario.Name))
                {
                    ModernMessageDialog.ShowWarning("시나리오 이름을 입력해주세요.", "알림");
                }
                return;
            }

            try
            {
                await _testDatabaseService.UpdateScenarioAsync(scenario);
                scenario.IsEditing = false;
            }
            catch (System.Exception ex)
            {
                ModernMessageDialog.ShowError($"시나리오 이름 변경 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void CancelRename(TestScenario? scenario)
        {
            if (scenario == null) return;

            scenario.IsEditing = false;

            // 원래 이름으로 복원 (데이터베이스에서 다시 로드)
            _ = Task.Run(async () =>
            {
                try
                {
                    var originalScenario = await _testDatabaseService.GetScenarioByIdAsync(scenario.Id);
                    if (originalScenario != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            scenario.Name = originalScenario.Name;
                        });
                    }
                }
                catch (System.Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ModernMessageDialog.ShowError($"원래 이름을 복원하는 중 오류가 발생했습니다: {ex.Message}");
                    });
                }
            });
        }

        private void OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dataItem = new ReceivedDataItem
                {
                    Timestamp = e.Timestamp,
                    IpAddress = e.IpAddress,
                    Port = e.Port,
                    Data = Convert.ToHexString(e.Data)
                };

                ReceivedDataItems.Add(dataItem);

                // Keep only last 1000 items to avoid memory issues
                if (ReceivedDataItems.Count > 1000)
                {
                    ReceivedDataItems.RemoveAt(0);
                }
            });
        }

        private void OnLogAdded(object? sender, TestLogEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogItems.Add(e.LogItem);

                // 최대 500개 로그만 유지
                if (LogItems.Count > 500)
                {
                    LogItems.RemoveAt(0);
                }
            });
        }

        private void ClearLogInternal()
        {
            LogItems.Clear();
        }

        public void AddCustomLog(LogLevel level, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var logItem = TestLogItem.Create(level, message);
                LogItems.Add(logItem);
            });
        }

        // 새로 추가된 메서드들
        private async Task RunSingleStep(TestStep step)
        {
            if (step == null) return;

            try
            {
                // UI 상태 업데이트
                step.IsRunning = true;
                step.HasFailed = false;
                step.LastErrorMessage = string.Empty;

                // 단일 스텝 실행
                var result = await _testExecutionService.RunSingleStepAsync(step);

                // 결과에 따른 UI 상태 업데이트
                step.LastResult = result;

                if (!result.IsSuccess)
                {
                    step.HasFailed = true;
                    step.LastErrorMessage = result.ErrorMessage ?? "알 수 없는 오류가 발생했습니다.";

                    // 실패 시 사용자에게 알림
                    ModernMessageDialog.ShowWarning($"스텝 '{step.Name}' 실행이 실패했습니다.\n\n오류: {step.LastErrorMessage}", "스텝 실행 실패");
                }
                else
                {
                    step.HasFailed = false;
                    step.LastErrorMessage = string.Empty;

                    // 성공 시 간단한 알림
                    ModernMessageDialog.ShowSuccess($"스텝 '{step.Name}'이 성공적으로 실행되었습니다.", "스텝 실행 완료");
                }
            }
            catch (Exception ex)
            {
                // 예외 발생 시 UI 상태 업데이트
                step.HasFailed = true;
                step.LastErrorMessage = ex.Message;

                ModernMessageDialog.ShowError($"스텝 '{step.Name}' 실행 중 오류가 발생했습니다.\n\n{ex.Message}");
            }
            finally
            {
                // 실행 완료 후 UI 상태 정리
                step.IsRunning = false;
            }
        }

        private async Task DeleteSingleStep(TestStep step)
        {
            if (step == null) return;

            try
            {
                // 기존 DeleteStep 로직 사용
                SelectedStep = step;
                await DeleteStepInternal();
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"테스트 스텝 삭제 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private void OpenDetailedResultsInternal()
        {
            // 상세 결과 창 열기 로직
            ModernMessageDialog.ShowInfo("상세 결과 창을 구현해야 합니다.", "정보");
        }

        private Task RefreshResultsInternal()
        {
            try
            {
                // 결과 통계 업데이트
                SuccessfulTests = 0;
                FailedTests = 0;

                foreach (var scenario in Scenarios)
                {
                    // 각 시나리오의 마지막 실행 결과를 확인
                    // 실제 구현에서는 데이터베이스에서 결과를 가져와야 함
                }

                LastUpdateTime = DateTime.Now;
                OnPropertyChanged(nameof(TotalScenarios));
                OnPropertyChanged(nameof(SuccessfulTests));
                OnPropertyChanged(nameof(FailedTests));
                OnPropertyChanged(nameof(SuccessRate));
                OnPropertyChanged(nameof(LastUpdateTime));

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ModernMessageDialog.ShowError($"결과 새로고침 중 오류가 발생했습니다: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        #endregion
    }
}