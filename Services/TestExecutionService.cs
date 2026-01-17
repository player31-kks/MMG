using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MMG.Models;
using MMG.Services;

namespace MMG.Services
{
    /// <summary>
    /// 테스트 로그 이벤트 인자
    /// </summary>
    public class TestLogEventArgs : EventArgs
    {
        public TestLogItem LogItem { get; set; } = null!;
    }

    /// <summary>
    /// 백그라운드 스텝 정보
    /// </summary>
    public class BackgroundStepInfo
    {
        public TestStep Step { get; set; } = null!;
        public Task Task { get; set; } = null!;
        public CancellationTokenSource CancellationTokenSource { get; set; } = null!;
    }

    public class TestExecutionService
    {
        private readonly UdpClientService _udpClientService;
        private readonly DatabaseService _databaseService;
        private readonly TestDatabaseService _testDatabaseService;
        private readonly UdpSocketManager _socketManager;
        private CancellationTokenSource? _cancellationTokenSource;
        
        // 백그라운드 스텝 관리
        private readonly ConcurrentDictionary<int, BackgroundStepInfo> _backgroundSteps = new();
        
        // 시나리오 시작 시간 (절대 지연 계산용)
        private DateTime _scenarioStartTime;
        private Stopwatch? _scenarioStopwatch;

        // 시나리오 레벨 바인딩 클라이언트
        private ManagedUdpClient? _scenarioClient;
        private int _scenarioBindPort;

        public event EventHandler<TestProgressEventArgs>? ProgressChanged;
        public event EventHandler<TestCompletedEventArgs>? TestCompleted;
        public event EventHandler<DataReceivedEventArgs>? DataReceived;
        public event EventHandler<TestLogEventArgs>? LogAdded;

        public bool IsRunning => _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested;

        public TestExecutionService(
            UdpClientService udpClientService,
            DatabaseService databaseService,
            TestDatabaseService testDatabaseService)
        {
            _udpClientService = udpClientService;
            _databaseService = databaseService;
            _testDatabaseService = testDatabaseService;
            _socketManager = UdpSocketManager.Instance;

            // Subscribe to data received events
            _udpClientService.DataReceived += (sender, args) => DataReceived?.Invoke(this, args);
        }

        private void AddLog(LogLevel level, string message, string stepName = "", string details = "")
        {
            var logItem = TestLogItem.Create(level, message, stepName, details);
            LogAdded?.Invoke(this, new TestLogEventArgs { LogItem = logItem });
        }

        public async Task RunScenarioAsync(TestScenario scenario)
        {
            if (_cancellationTokenSource != null)
            {
                throw new InvalidOperationException("이미 실행 중인 테스트가 있습니다.");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            _scenarioStartTime = DateTime.Now;
            _scenarioStopwatch = Stopwatch.StartNew();
            _backgroundSteps.Clear();

            // 시나리오 레벨 포트 바인딩
            _scenarioClient = null;
            _scenarioBindPort = 0;

            try
            {
                AddLog(LogLevel.Info, "테스트 시나리오 로딩...", scenario.Name);
                ReportProgress("테스트 시나리오 로딩...", 0);

                // 시나리오 레벨 포트 바인딩
                if (scenario.UseBindPort)
                {
                    _scenarioBindPort = scenario.BindPort;
                    _scenarioClient = _socketManager.BindPort(_scenarioBindPort);
                    _scenarioBindPort = _scenarioClient.ActualPort; // 실제 할당된 포트
                    
                    _scenarioClient.DataReceived += OnScenarioDataReceived;
                    
                    AddLog(LogLevel.Info, $"시나리오 포트 바인딩: {_scenarioBindPort}", scenario.Name);
                }

                // 데이터베이스에서 최신 스텝 정보를 가져옴
                var latestSteps = await _testDatabaseService.GetStepsForScenarioAsync(scenario.Id);

                int totalSteps = latestSteps.Count;
                int completedSteps = 0;
                int successfulSteps = 0;
                int failedSteps = 0;

                AddLog(LogLevel.Info, $"테스트 시나리오 시작 - 총 {totalSteps}개 스텝", scenario.Name);
                ReportProgress("테스트 시나리오 시작...", 0);

                // 스텝 실행
                foreach (var step in latestSteps)
                {
                    token.ThrowIfCancellationRequested();

                    if (!step.IsEnabled)
                    {
                        completedSteps++;
                        AddLog(LogLevel.Warning, $"스텝 건너뜀 (비활성화)", step.Name);
                        ReportProgress($"스텝 '{step.Name}' 건너뜀 (비활성화)",
                                       (double)completedSteps / totalSteps * 100);
                        continue;
                    }

                    // 백그라운드 스텝인 경우 - 즉시 비동기로 시작하고 다음 스텝으로 진행
                    if (step.IsBackground)
                    {
                        AddLog(LogLevel.Info, $"백그라운드 스텝 시작 [{step.StepTypeDisplay}]", step.Name);
                        step.StatusText = "백그라운드 실행 중...";

                        var bgCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                        // 백그라운드 스텝은 내부에서 절대 지연 등을 처리함
                        var bgTask = ExecuteBackgroundStepAsync(step, _scenarioStopwatch!, bgCts.Token);
                        
                        _backgroundSteps.TryAdd(step.Id, new BackgroundStepInfo
                        {
                            Step = step,
                            Task = bgTask,
                            CancellationTokenSource = bgCts
                        });

                        completedSteps++;
                        successfulSteps++; // 백그라운드 시작은 성공으로 처리
                        ReportProgress($"백그라운드 스텝 '{step.Name}' 시작됨",
                                       (double)completedSteps / totalSteps * 100);
                        
                        // 백그라운드는 다음 스텝으로 바로 진행
                        continue;
                    }

                    // 순차 스텝 실행 (메인 타임라인)
                    
                    // 절대 지연 타입인 경우, 시나리오 시작부터의 시간 계산
                    if (step.StepType == "AbsoluteDelayed" && step.StartDelayFromScenarioMs > 0)
                    {
                        var elapsedMs = _scenarioStopwatch!.ElapsedMilliseconds;
                        var remainingDelay = step.StartDelayFromScenarioMs - elapsedMs;
                        
                        if (remainingDelay > 0)
                        {
                            AddLog(LogLevel.Debug, $"절대 지연 대기: {remainingDelay}ms (시나리오 시작 후 {step.StartDelayFromScenarioMs}ms 시점)", step.Name);
                            await Task.Delay((int)remainingDelay, token);
                        }
                    }

                    AddLog(LogLevel.Info, $"스텝 실행 시작 [{step.StepTypeDisplay}]", step.Name);
                    step.StatusText = "실행 중...";

                    var stepResult = await ExecuteStepAsync(step, token);

                    // Save test result to database
                    await _testDatabaseService.SaveTestResultAsync(stepResult);

                    if (stepResult.IsSuccess)
                    {
                        successfulSteps++;
                        step.StatusText = "완료";
                        AddLog(LogLevel.Success, $"스텝 실행 성공 ({stepResult.ExecutionTimeMs:F0}ms)", step.Name);
                    }
                    else
                    {
                        failedSteps++;
                        step.StatusText = $"실패: {stepResult.ErrorMessage}";
                        AddLog(LogLevel.Error, $"스텝 실행 실패: {stepResult.ErrorMessage}", step.Name);
                    }

                    completedSteps++;
                    ReportProgress($"스텝 '{step.Name}' 완료",
                                   (double)completedSteps / totalSteps * 100);
                }

                // 모든 백그라운드 스텝 완료 대기
                if (_backgroundSteps.Any())
                {
                    AddLog(LogLevel.Info, $"백그라운드 스텝 완료 대기 중... ({_backgroundSteps.Count}개)", scenario.Name);
                    
                    try
                    {
                        await Task.WhenAll(_backgroundSteps.Values.Select(b => b.Task));
                    }
                    catch (OperationCanceledException)
                    {
                        // 취소된 경우 무시
                    }

                    AddLog(LogLevel.Info, "모든 백그라운드 스텝 완료", scenario.Name);
                }

                _scenarioStopwatch.Stop();

                // Update scenario last run time
                scenario.LastRunAt = DateTime.Now;

                var completedArgs = new TestCompletedEventArgs
                {
                    TotalSteps = totalSteps,
                    SuccessfulSteps = successfulSteps,
                    FailedSteps = failedSteps,
                    TotalExecutionTime = _scenarioStopwatch.Elapsed,
                    Summary = $"테스트 완료: {successfulSteps}/{totalSteps} 성공"
                };

                AddLog(LogLevel.Success, $"시나리오 완료: {successfulSteps}/{totalSteps} 성공, 총 실행시간: {_scenarioStopwatch.Elapsed.TotalSeconds:F2}초", scenario.Name);
                TestCompleted?.Invoke(this, completedArgs);
            }
            catch (OperationCanceledException)
            {
                // 모든 백그라운드 스텝 취소
                foreach (var bg in _backgroundSteps.Values)
                {
                    bg.CancellationTokenSource.Cancel();
                }

                AddLog(LogLevel.Warning, "테스트가 사용자에 의해 중지되었습니다.", scenario.Name);
                ReportProgress("테스트가 중지되었습니다.", 100);
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"테스트 실행 중 오류: {ex.Message}", scenario.Name, ex.StackTrace ?? "");
                ReportProgress($"테스트 실행 중 오류: {ex.Message}", 100);
            }
            finally
            {
                // 모든 백그라운드 스텝 정리
                foreach (var bg in _backgroundSteps.Values)
                {
                    bg.CancellationTokenSource.Cancel();
                    bg.CancellationTokenSource.Dispose();
                }
                _backgroundSteps.Clear();

                // 시나리오 레벨 포트 바인딩 해제
                if (_scenarioClient != null)
                {
                    _scenarioClient.DataReceived -= OnScenarioDataReceived;
                    _socketManager.UnbindPort(_scenarioBindPort);
                    _scenarioClient = null;
                    AddLog(LogLevel.Info, $"시나리오 포트 해제: {_scenarioBindPort}", scenario.Name);
                }

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _scenarioStopwatch = null;
            }
        }

        private void OnScenarioDataReceived(object? sender, DataReceivedEventArgs e)
        {
            DataReceived?.Invoke(this, e);
        }

        /// <summary>
        /// 백그라운드 스텝 실행 (비동기로 실행되며, 절대 지연은 내부에서 처리)
        /// </summary>
        private async Task ExecuteBackgroundStepAsync(TestStep step, Stopwatch scenarioStopwatch, CancellationToken cancellationToken)
        {
            try
            {
                // 절대 지연 타입인 경우, 시나리오 시작부터의 시간 계산하여 대기
                if (step.StepType == "AbsoluteDelayed" && step.StartDelayFromScenarioMs > 0)
                {
                    var elapsedMs = scenarioStopwatch.ElapsedMilliseconds;
                    var remainingDelay = step.StartDelayFromScenarioMs - elapsedMs;
                    
                    if (remainingDelay > 0)
                    {
                        AddLog(LogLevel.Debug, $"[백그라운드] 절대 지연 대기: {remainingDelay}ms (시나리오 시작 후 {step.StartDelayFromScenarioMs}ms 시점)", step.Name);
                        await Task.Delay((int)remainingDelay, cancellationToken);
                    }
                }

                var result = await ExecuteStepAsync(step, cancellationToken);
                
                if (result.IsSuccess)
                {
                    step.StatusText = "백그라운드 완료";
                    AddLog(LogLevel.Success, $"백그라운드 스텝 완료 ({result.ExecutionTimeMs:F0}ms)", step.Name);
                }
                else
                {
                    step.StatusText = $"백그라운드 실패: {result.ErrorMessage}";
                    AddLog(LogLevel.Error, $"백그라운드 스텝 실패: {result.ErrorMessage}", step.Name);
                }

                await _testDatabaseService.SaveTestResultAsync(result);
            }
            catch (OperationCanceledException)
            {
                step.StatusText = "백그라운드 취소됨";
                AddLog(LogLevel.Warning, "백그라운드 스텝이 취소되었습니다.", step.Name);
            }
            catch (Exception ex)
            {
                step.StatusText = $"백그라운드 오류: {ex.Message}";
                AddLog(LogLevel.Error, $"백그라운드 스텝 오류: {ex.Message}", step.Name);
            }
        }

        private async Task<TestResult> ExecuteStepAsync(TestStep step, CancellationToken cancellationToken)
        {
            var result = new TestResult
            {
                ScenarioId = step.ScenarioId,
                StepId = step.Id,
                ExecutedAt = DateTime.Now
            };

            var stepStopwatch = Stopwatch.StartNew();

            try
            {
                // StepType에 따라 실행 방식 결정
                switch (step.StepType)
                {
                    case "Immediate":
                        await ExecuteImmediateRequest(step, result, cancellationToken);
                        break;

                    case "PreDelayed":
                        await ExecutePreDelayedRequest(step, result, cancellationToken);
                        break;

                    case "PostDelayed":
                        await ExecutePostDelayedRequest(step, result, cancellationToken);
                        break;

                    case "Periodic":
                        await ExecutePeriodicRequest(step, result, cancellationToken);
                        break;

                    case "AbsoluteDelayed":
                        // 절대 지연은 이미 RunScenarioAsync에서 처리됨
                        await ExecuteImmediateRequest(step, result, cancellationToken);
                        break;

                    case "WaitForMessage":
                        await ExecuteWaitForMessage(step, result, cancellationToken);
                        break;

                    case "ReceiveAndReply":
                        await ExecuteReceiveAndReply(step, result, cancellationToken);
                        break;

                    // 하위 호환성을 위한 기존 타입 지원
                    case "SingleRequest":
                        await ExecuteImmediateRequest(step, result, cancellationToken);
                        break;

                    case "DelayedRequest":
                        await ExecutePreDelayedRequest(step, result, cancellationToken);
                        break;

                    case "PeriodicRequest":
                        await ExecutePeriodicRequest(step, result, cancellationToken);
                        break;

                    default:
                        throw new Exception($"알 수 없는 스텝 유형: {step.StepType}");
                }

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
            }

            stepStopwatch.Stop();
            result.ExecutionTimeMs = stepStopwatch.Elapsed.TotalMilliseconds;

            return result;
        }

        /// <summary>
        /// SavedRequest 가져오기
        /// </summary>
        private async Task<SavedRequest> GetSavedRequestAsync(int savedRequestId)
        {
            var savedRequests = await _databaseService.GetAllRequestsAsync();
            var savedRequest = savedRequests.FirstOrDefault(r => r.Id == savedRequestId);

            if (savedRequest == null)
            {
                throw new Exception($"저장된 요청을 찾을 수 없습니다. ID: {savedRequestId}");
            }

            return savedRequest;
        }

        /// <summary>
        /// 즉시 실행 - 지연 없이 바로 요청 전송
        /// </summary>
        private async Task ExecuteImmediateRequest(TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            var savedRequest = await GetSavedRequestAsync(step.SavedRequestId);

            AddLog(LogLevel.Debug, $"요청 전송: {savedRequest.IpAddress}:{savedRequest.Port}", step.Name);

            result.RequestSent = $"{savedRequest.IpAddress}:{savedRequest.Port}";

            // 시나리오 레벨 클라이언트가 있으면 사용
            if (_scenarioClient != null)
            {
                result.RequestSent += $" (로컬 포트: {_scenarioBindPort})";
                var response = await SendWithScenarioClientAsync(savedRequest, step.Name);
                ProcessResponse(response, step, result);
            }
            else
            {
                var udpRequest = CreateUdpRequestFromSaved(savedRequest);
                var response = await _udpClientService.SendRequestAsync(udpRequest);
                ProcessResponse(response, step, result);
            }
        }

        /// <summary>
        /// 시나리오 레벨 클라이언트로 송신
        /// </summary>
        private async Task<UdpResponse> SendWithScenarioClientAsync(SavedRequest savedRequest, string stepName)
        {
            var response = new UdpResponse();

            try
            {
                var messageBytes = BuildMessageFromSavedRequest(savedRequest);
                var endpoint = new IPEndPoint(IPAddress.Parse(savedRequest.IpAddress), savedRequest.Port);

                // 수신 큐 비우기
                _scenarioClient!.ClearReceiveQueue();

                // 송신
                await _scenarioClient.SendAsync(messageBytes, endpoint);
                response.Status = "Sent";

                // 응답 대기
                var receiveResult = await _scenarioClient.WaitForResponseAsync(5000);

                if (receiveResult != null)
                {
                    response.RawData = receiveResult.Value.Data;
                    response.Status = "Success";
                    AddLog(LogLevel.Debug, $"응답 수신: {receiveResult.Value.Data.Length} bytes from {receiveResult.Value.RemoteEndPoint}", stepName);
                }
                else
                {
                    response.Status = "Timeout";
                }
            }
            catch (Exception ex)
            {
                response.Status = $"Error: {ex.Message}";
            }

            response.ReceivedAt = DateTime.Now;
            return response;
        }

        /// <summary>
        /// SavedRequest에서 메시지 바이트 빌드
        /// </summary>
        private byte[] BuildMessageFromSavedRequest(SavedRequest savedRequest)
        {
            var udpRequest = CreateUdpRequestFromSaved(savedRequest);
            return _socketManager.BuildMessage(udpRequest.Headers, udpRequest.Payload, udpRequest.IsBigEndian);
        }

        /// <summary>
        /// 사전 지연 실행 - 지정된 시간(ms) 대기 후 요청 전송
        /// </summary>
        private async Task ExecutePreDelayedRequest(TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            var savedRequest = await GetSavedRequestAsync(step.SavedRequestId);

            int delayMs = step.PreDelayMs;

            // 하위 호환성: DelaySeconds가 설정되어 있으면 사용
            if (delayMs <= 0 && step.DelaySeconds > 0)
            {
                delayMs = (int)(step.DelaySeconds * 1000);
            }

            if (delayMs > 0)
            {
                AddLog(LogLevel.Debug, $"사전 지연 대기: {delayMs}ms", step.Name);
                result.RequestSent = $"{savedRequest.IpAddress}:{savedRequest.Port} (사전 지연: {delayMs}ms)";
                await Task.Delay(delayMs, cancellationToken);
            }

            // 지연 후 요청 실행
            await ExecuteImmediateRequest(step, result, cancellationToken);
        }

        /// <summary>
        /// 사후 지연 실행 - 요청 전송 후 지정된 시간(ms) 대기
        /// </summary>
        private async Task ExecutePostDelayedRequest(TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            // 먼저 요청 실행
            await ExecuteImmediateRequest(step, result, cancellationToken);

            int delayMs = step.PostDelayMs;

            if (delayMs > 0)
            {
                AddLog(LogLevel.Debug, $"사후 지연 대기: {delayMs}ms", step.Name);
                result.RequestSent += $" (사후 지연: {delayMs}ms)";
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        /// <summary>
        /// 주기적 실행 - 지정된 간격(ms)으로 반복 실행
        /// </summary>
        private async Task ExecutePeriodicRequest(TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            var savedRequest = await GetSavedRequestAsync(step.SavedRequestId);

            int intervalMs = step.IntervalMs;
            int repeatCount = step.RepeatCount;
            int durationMs = step.DurationMs;

            // 하위 호환성: FrequencyHz가 설정되어 있으면 사용
            if (intervalMs <= 0 && step.FrequencyHz > 0)
            {
                intervalMs = (int)(1000.0 / step.FrequencyHz);
            }

            // 하위 호환성: DurationSeconds가 설정되어 있으면 사용
            if (durationMs <= 0 && step.DurationSeconds > 0)
            {
                durationMs = (int)(step.DurationSeconds * 1000);
            }

            // RepeatCount가 설정되어 있으면 횟수 기반 실행
            // 아니면 DurationMs 기반으로 횟수 계산
            int totalExecutions;
            if (repeatCount > 0)
            {
                totalExecutions = repeatCount;
            }
            else if (durationMs > 0 && intervalMs > 0)
            {
                totalExecutions = durationMs / intervalMs;
            }
            else
            {
                totalExecutions = 1; // 기본값
            }

            if (intervalMs <= 0)
            {
                intervalMs = 100; // 기본 간격 100ms
            }

            AddLog(LogLevel.Info, $"주기적 실행 시작: {totalExecutions}회, 간격 {intervalMs}ms", step.Name);

            var responses = new System.Text.StringBuilder();
            int executionCount = 0;
            var startTime = DateTime.Now;

            for (int i = 0; i < totalExecutions && !cancellationToken.IsCancellationRequested; i++)
            {
                // 첫 실행이 아니면 간격 대기
                if (i > 0)
                {
                    await Task.Delay(intervalMs, cancellationToken);
                }

                // 요청 실행
                var udpRequest = CreateUdpRequestFromSaved(savedRequest);
                var response = await _udpClientService.SendRequestAsync(udpRequest);
                executionCount++;

                string responseText = response != null && response.RawData != null
                    ? BitConverter.ToString(response.RawData).Replace("-", " ")
                    : "응답 없음";

                responses.AppendLine($"[{executionCount}/{totalExecutions}] {DateTime.Now:HH:mm:ss.fff}: {responseText}");

                // 주기적으로 로그 출력 (10% 간격 또는 마지막)
                if (executionCount % Math.Max(1, totalExecutions / 10) == 0 || executionCount == totalExecutions)
                {
                    AddLog(LogLevel.Debug, $"주기적 요청 진행: {executionCount}/{totalExecutions}", step.Name);
                }
            }

            result.RequestSent = $"{savedRequest.IpAddress}:{savedRequest.Port} (주기적 실행 {executionCount}회, 간격 {intervalMs}ms)";
            result.ResponseReceived = responses.ToString();

            AddLog(LogLevel.Info, $"주기적 실행 완료: {executionCount}회 실행", step.Name);
        }

        /// <summary>
        /// 메시지 수신 대기 (서버 모드)
        /// </summary>
        private async Task ExecuteWaitForMessage(TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            int timeoutMs = step.ReceiveTimeoutMs;

            // 시나리오 클라이언트가 있으면 사용
            if (_scenarioClient != null)
            {
                AddLog(LogLevel.Info, $"메시지 수신 대기 시작 (시나리오 포트: {_scenarioBindPort}), 타임아웃: {timeoutMs}ms", step.Name);
                result.RequestSent = $"수신 대기 - 시나리오 포트: {_scenarioBindPort}";

                var receiveResult = await _scenarioClient.WaitForResponseAsync(timeoutMs);

                if (receiveResult != null)
                {
                    var responseText = BitConverter.ToString(receiveResult.Value.Data).Replace("-", " ");
                    result.ResponseReceived = $"수신됨 from {receiveResult.Value.RemoteEndPoint}: {responseText}";
                    AddLog(LogLevel.Success, $"메시지 수신 완료: {receiveResult.Value.Data.Length} bytes from {receiveResult.Value.RemoteEndPoint}", step.Name);

                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        IpAddress = receiveResult.Value.RemoteEndPoint.Address.ToString(),
                        Port = receiveResult.Value.RemoteEndPoint.Port,
                        Data = receiveResult.Value.Data,
                        Timestamp = DateTime.Now
                    });
                }
                else
                {
                    throw new TimeoutException($"수신 타임아웃 ({timeoutMs}ms)");
                }
                return;
            }

            // 시나리오 클라이언트 없으면 스텝별 포트 사용
            int listenPort = step.ListenPort;

            if (listenPort <= 0)
            {
                throw new Exception("수신 대기 포트가 설정되지 않았습니다. 시나리오 포트 바인딩을 사용하거나 스텝에 수신 포트를 설정하세요.");
            }

            AddLog(LogLevel.Info, $"메시지 수신 대기 시작 - 포트: {listenPort}, 타임아웃: {timeoutMs}ms", step.Name);
            result.RequestSent = $"수신 대기 - 포트: {listenPort}";

            using var udpClient = new UdpClient(listenPort);
            
            try
            {
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var receiveTask = udpClient.ReceiveAsync();
                var delayTask = Task.Delay(timeoutMs, linkedCts.Token);

                var completedTask = await Task.WhenAny(receiveTask, delayTask);

                if (completedTask == receiveTask)
                {
                    var udpResult = await receiveTask;
                    var responseText = BitConverter.ToString(udpResult.Buffer).Replace("-", " ");
                    result.ResponseReceived = $"수신됨 from {udpResult.RemoteEndPoint}: {responseText}";
                    AddLog(LogLevel.Success, $"메시지 수신 완료: {udpResult.Buffer.Length} bytes from {udpResult.RemoteEndPoint}", step.Name);

                    // DataReceived 이벤트 발생
                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        IpAddress = udpResult.RemoteEndPoint.Address.ToString(),
                        Port = udpResult.RemoteEndPoint.Port,
                        Data = udpResult.Buffer,
                        Timestamp = DateTime.Now
                    });
                }
                else
                {
                    throw new TimeoutException($"수신 타임아웃 ({timeoutMs}ms)");
                }
            }
            finally
            {
                udpClient.Close();
            }
        }

        /// <summary>
        /// 메시지 수신 후 응답 전송 (서버 모드)
        /// </summary>
        private async Task ExecuteReceiveAndReply(TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            int timeoutMs = step.ReceiveTimeoutMs;
            int responseRequestId = step.ResponseRequestId;

            if (responseRequestId <= 0)
            {
                throw new Exception("응답 요청 ID가 설정되지 않았습니다.");
            }

            // 시나리오 클라이언트가 있으면 사용
            if (_scenarioClient != null)
            {
                AddLog(LogLevel.Info, $"수신 후 응답 대기 (시나리오 포트: {_scenarioBindPort}), 타임아웃: {timeoutMs}ms", step.Name);
                result.RequestSent = $"수신 대기 (응답 예정) - 시나리오 포트: {_scenarioBindPort}";

                var receiveResult = await _scenarioClient.WaitForResponseAsync(timeoutMs);

                if (receiveResult != null)
                {
                    var receivedText = BitConverter.ToString(receiveResult.Value.Data).Replace("-", " ");
                    AddLog(LogLevel.Info, $"메시지 수신: {receiveResult.Value.Data.Length} bytes from {receiveResult.Value.RemoteEndPoint}", step.Name);

                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        IpAddress = receiveResult.Value.RemoteEndPoint.Address.ToString(),
                        Port = receiveResult.Value.RemoteEndPoint.Port,
                        Data = receiveResult.Value.Data,
                        Timestamp = DateTime.Now
                    });

                    // 응답 전송 (시나리오 클라이언트 사용)
                    var responseRequest = await GetSavedRequestAsync(responseRequestId);
                    var messageBytes = BuildMessageFromSavedRequest(responseRequest);
                    var replyEndpoint = receiveResult.Value.RemoteEndPoint;

                    AddLog(LogLevel.Debug, $"응답 전송: {replyEndpoint}", step.Name);
                    await _scenarioClient.SendAsync(messageBytes, replyEndpoint);

                    result.ResponseReceived = $"수신: {receivedText}\n응답 전송: {replyEndpoint}";
                    AddLog(LogLevel.Success, "수신 후 응답 전송 완료", step.Name);
                }
                else
                {
                    throw new TimeoutException($"수신 타임아웃 ({timeoutMs}ms)");
                }
                return;
            }

            // 시나리오 클라이언트 없으면 스텝별 포트 사용
            int listenPort = step.ListenPort;

            if (listenPort <= 0)
            {
                throw new Exception("수신 대기 포트가 설정되지 않았습니다. 시나리오 포트 바인딩을 사용하거나 스텝에 수신 포트를 설정하세요.");
            }

            AddLog(LogLevel.Info, $"수신 후 응답 대기 시작 - 포트: {listenPort}, 타임아웃: {timeoutMs}ms", step.Name);
            result.RequestSent = $"수신 대기 (응답 예정) - 포트: {listenPort}";

            using var udpClient = new UdpClient(listenPort);

            try
            {
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var receiveTask = udpClient.ReceiveAsync();
                var delayTask = Task.Delay(timeoutMs, linkedCts.Token);

                var completedTask = await Task.WhenAny(receiveTask, delayTask);

                if (completedTask == receiveTask)
                {
                    var udpResult = await receiveTask;
                    var receivedText = BitConverter.ToString(udpResult.Buffer).Replace("-", " ");
                    AddLog(LogLevel.Info, $"메시지 수신: {udpResult.Buffer.Length} bytes from {udpResult.RemoteEndPoint}", step.Name);

                    // DataReceived 이벤트 발생
                    DataReceived?.Invoke(this, new DataReceivedEventArgs
                    {
                        IpAddress = udpResult.RemoteEndPoint.Address.ToString(),
                        Port = udpResult.RemoteEndPoint.Port,
                        Data = udpResult.Buffer,
                        Timestamp = DateTime.Now
                    });

                    // 응답 전송
                    var responseRequest = await GetSavedRequestAsync(responseRequestId);
                    var responseUdpRequest = CreateUdpRequestFromSaved(responseRequest);

                    // 응답은 수신된 주소로 전송
                    responseUdpRequest.IpAddress = udpResult.RemoteEndPoint.Address.ToString();
                    responseUdpRequest.Port = udpResult.RemoteEndPoint.Port;

                    AddLog(LogLevel.Debug, $"응답 전송: {responseUdpRequest.IpAddress}:{responseUdpRequest.Port}", step.Name);

                    var response = await _udpClientService.SendRequestAsync(responseUdpRequest);

                    result.ResponseReceived = $"수신: {receivedText}\n응답 전송: {responseRequest.IpAddress}:{responseRequest.Port}";
                    AddLog(LogLevel.Success, "수신 후 응답 전송 완료", step.Name);
                }
                else
                {
                    throw new TimeoutException($"수신 타임아웃 ({timeoutMs}ms)");
                }
            }
            finally
            {
                udpClient.Close();
            }
        }

        /// <summary>
        /// 응답 처리
        /// </summary>
        private void ProcessResponse(UdpResponse? response, TestStep step, TestResult result)
        {
            if (response == null)
            {
                result.ResponseReceived = "응답 없음";
                AddLog(LogLevel.Warning, "응답 없음", step.Name);
                throw new Exception("UDP 응답을 받지 못했습니다.");
            }

            // 응답 상태 확인
            if (!string.IsNullOrEmpty(response.Status))
            {
                if (response.Status.StartsWith("Error:"))
                {
                    var errorMessage = response.Status.Substring(6).Trim();
                    result.ResponseReceived = response.Status;
                    AddLog(LogLevel.Error, $"요청 실패: {errorMessage}", step.Name);
                    throw new Exception(errorMessage);
                }
                else if (response.Status == "Timeout")
                {
                    result.ResponseReceived = "타임아웃";
                    AddLog(LogLevel.Warning, "응답 타임아웃 (5초)", step.Name);
                    throw new Exception("UDP 응답 타임아웃 (5초)");
                }
            }

            // 성공적인 응답 처리
            if (response.RawData != null && response.RawData.Length > 0)
            {
                var responseText = BitConverter.ToString(response.RawData).Replace("-", " ");
                result.ResponseReceived = responseText;
                AddLog(LogLevel.Debug, $"응답 수신: {response.RawData.Length} bytes", step.Name, responseText);
            }
            else
            {
                result.ResponseReceived = "빈 응답";
                AddLog(LogLevel.Warning, "빈 응답 수신", step.Name);
            }
        }

        private UdpRequest CreateUdpRequestFromSaved(SavedRequest savedRequest)
        {
            var udpRequest = new UdpRequest
            {
                IpAddress = savedRequest.IpAddress,
                Port = savedRequest.Port,
                IsBigEndian = savedRequest.IsBigEndian,
                UseCustomLocalPort = savedRequest.UseCustomLocalPort,
                CustomLocalPort = savedRequest.CustomLocalPort
            };

            // RequestSchemaJson이 있으면 파싱해서 Headers와 Payload 설정
            if (!string.IsNullOrEmpty(savedRequest.RequestSchemaJson))
            {
                try
                {
                    // RequestSchemaJson은 "HeadersJson|PayloadJson" 형태로 저장됨
                    var parts = savedRequest.RequestSchemaJson.Split('|');

                    if (parts.Length >= 1 && !string.IsNullOrEmpty(parts[0]))
                    {
                        // Headers 파싱
                        var headers = _databaseService.DeserializeDataFields(parts[0]);
                        udpRequest.Headers.Clear();
                        foreach (var header in headers)
                        {
                            udpRequest.Headers.Add(header);
                        }
                    }

                    if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[1]))
                    {
                        // Payload 파싱
                        var payload = _databaseService.DeserializeDataFields(parts[1]);
                        udpRequest.Payload.Clear();
                        foreach (var data in payload)
                        {
                            udpRequest.Payload.Add(data);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // 파싱 실패 시 로그를 남기고 기본 요청만 사용
                    System.Diagnostics.Debug.WriteLine($"Failed to parse RequestSchemaJson: {ex.Message}");
                }
            }

            return udpRequest;
        }

        public void StopTest()
        {
            // 모든 백그라운드 스텝 취소
            foreach (var bg in _backgroundSteps.Values)
            {
                bg.CancellationTokenSource.Cancel();
            }

            _cancellationTokenSource?.Cancel();
        }

        public async Task<TestResult> RunSingleStepAsync(TestStep step)
        {
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token; // 5분 타임아웃
            ReportProgress($"단일 스텝 '{step.Name}' 실행 중...", 0);
            try
            {
                var result = await ExecuteStepAsync(step, cancellationToken);
                ReportProgress($"단일 스텝 '{step.Name}' 완료", 100);
                return result;
            }
            catch (Exception ex)
            {
                var errorResult = new TestResult
                {
                    ScenarioId = step.ScenarioId,
                    StepId = step.Id,
                    ExecutedAt = DateTime.Now,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ExecutionTimeMs = 0
                };
                ReportProgress($"단일 스텝 '{step.Name}' 실패: {ex.Message}", 100);
                return errorResult;
            }
        }

        private void ReportProgress(string message, double percentage)
        {
            ProgressChanged?.Invoke(this, new TestProgressEventArgs
            {
                Message = message,
                ProgressPercentage = percentage,
                IsCompleted = percentage >= 100
            });
        }
    }
}
