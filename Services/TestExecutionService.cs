using System;
using System.Diagnostics;
using System.Linq;
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

    public class TestExecutionService
    {
        private readonly UdpClientService _udpClientService;
        private readonly DatabaseService _databaseService;
        private readonly TestDatabaseService _testDatabaseService;
        private CancellationTokenSource? _cancellationTokenSource;

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

            var stopwatch = Stopwatch.StartNew();

            try
            {
                AddLog(LogLevel.Info, "테스트 시나리오 로딩...", scenario.Name);
                ReportProgress("테스트 시나리오 로딩...", 0);

                // 데이터베이스에서 최신 스텝 정보를 가져옴
                var latestSteps = await _testDatabaseService.GetStepsForScenarioAsync(scenario.Id);

                int totalSteps = latestSteps.Count;
                int completedSteps = 0;
                int successfulSteps = 0;
                int failedSteps = 0;

                AddLog(LogLevel.Info, $"테스트 시나리오 시작 - 총 {totalSteps}개 스텝", scenario.Name);
                ReportProgress("테스트 시나리오 시작...", 0);

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

                stopwatch.Stop();

                // Update scenario last run time
                scenario.LastRunAt = DateTime.Now;

                var completedArgs = new TestCompletedEventArgs
                {
                    TotalSteps = totalSteps,
                    SuccessfulSteps = successfulSteps,
                    FailedSteps = failedSteps,
                    TotalExecutionTime = stopwatch.Elapsed,
                    Summary = $"테스트 완료: {successfulSteps}/{totalSteps} 성공"
                };

                AddLog(LogLevel.Success, $"시나리오 완료: {successfulSteps}/{totalSteps} 성공, 총 실행시간: {stopwatch.Elapsed.TotalSeconds:F2}초", scenario.Name);
                TestCompleted?.Invoke(this, completedArgs);
            }
            catch (OperationCanceledException)
            {
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
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
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
                // Get the saved request
                var savedRequests = await _databaseService.GetAllRequestsAsync();
                var savedRequest = savedRequests.FirstOrDefault(r => r.Id == step.SavedRequestId);

                if (savedRequest == null)
                {
                    throw new Exception($"저장된 요청을 찾을 수 없습니다. ID: {step.SavedRequestId}");
                }

                // StepType에 따라 실행 방식 결정
                switch (step.StepType)
                {
                    case "Immediate":
                        await ExecuteImmediateRequest(savedRequest, step, result, cancellationToken);
                        break;

                    case "PreDelayed":
                        await ExecutePreDelayedRequest(savedRequest, step, result, cancellationToken);
                        break;

                    case "PostDelayed":
                        await ExecutePostDelayedRequest(savedRequest, step, result, cancellationToken);
                        break;

                    case "Periodic":
                        await ExecutePeriodicRequest(savedRequest, step, result, cancellationToken);
                        break;

                    // 하위 호환성을 위한 기존 타입 지원
                    case "SingleRequest":
                        await ExecuteImmediateRequest(savedRequest, step, result, cancellationToken);
                        break;

                    case "DelayedRequest":
                        await ExecutePreDelayedRequest(savedRequest, step, result, cancellationToken);
                        break;

                    case "PeriodicRequest":
                        await ExecutePeriodicRequest(savedRequest, step, result, cancellationToken);
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
        /// 즉시 실행 - 지연 없이 바로 요청 전송
        /// </summary>
        private async Task ExecuteImmediateRequest(SavedRequest savedRequest, TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            AddLog(LogLevel.Debug, $"요청 전송: {savedRequest.IpAddress}:{savedRequest.Port}", step.Name);

            result.RequestSent = $"{savedRequest.IpAddress}:{savedRequest.Port}";

            var udpRequest = CreateUdpRequestFromSaved(savedRequest);
            var response = await _udpClientService.SendRequestAsync(udpRequest);

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

        /// <summary>
        /// 사전 지연 실행 - 지정된 시간(ms) 대기 후 요청 전송
        /// </summary>
        private async Task ExecutePreDelayedRequest(SavedRequest savedRequest, TestStep step, TestResult result, CancellationToken cancellationToken)
        {
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
            await ExecuteImmediateRequest(savedRequest, step, result, cancellationToken);
        }

        /// <summary>
        /// 사후 지연 실행 - 요청 전송 후 지정된 시간(ms) 대기
        /// </summary>
        private async Task ExecutePostDelayedRequest(SavedRequest savedRequest, TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            // 먼저 요청 실행
            await ExecuteImmediateRequest(savedRequest, step, result, cancellationToken);

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
        private async Task ExecutePeriodicRequest(SavedRequest savedRequest, TestStep step, TestResult result, CancellationToken cancellationToken)
        {
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

                string responseText = response != null
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

        private UdpRequest CreateUdpRequestFromSaved(SavedRequest savedRequest)
        {
            var udpRequest = new UdpRequest
            {
                IpAddress = savedRequest.IpAddress,
                Port = savedRequest.Port
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