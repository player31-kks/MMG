using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MMG.Models;
using MMG.Services;

namespace MMG.Services
{
    public class TestExecutionService
    {
        private readonly UdpClientService _udpClientService;
        private readonly DatabaseService _databaseService;
        private readonly TestDatabaseService _testDatabaseService;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<TestProgressEventArgs>? ProgressChanged;
        public event EventHandler<TestCompletedEventArgs>? TestCompleted;
        public event EventHandler<DataReceivedEventArgs>? DataReceived;

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
                ReportProgress("테스트 시나리오 로딩...", 0);

                // 데이터베이스에서 최신 스텝 정보를 가져옴
                var latestSteps = await _testDatabaseService.GetStepsForScenarioAsync(scenario.Id);
                
                int totalSteps = latestSteps.Count;
                int completedSteps = 0;
                int successfulSteps = 0;
                int failedSteps = 0;

                ReportProgress("테스트 시나리오 시작...", 0);

                foreach (var step in latestSteps)
                {
                    token.ThrowIfCancellationRequested();

                    if (!step.IsEnabled)
                    {
                        completedSteps++;
                        ReportProgress($"스텝 '{step.Name}' 건너뜀 (비활성화)", 
                                       (double)completedSteps / totalSteps * 100);
                        continue;
                    }

                    var stepResult = await ExecuteStepAsync(step, token);
                    
                    // Save test result to database
                    await _testDatabaseService.SaveTestResultAsync(stepResult);

                    if (stepResult.IsSuccess)
                    {
                        successfulSteps++;
                    }
                    else
                    {
                        failedSteps++;
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

                TestCompleted?.Invoke(this, completedArgs);
            }
            catch (OperationCanceledException)
            {
                ReportProgress("테스트가 중지되었습니다.", 100);
            }
            catch (Exception ex)
            {
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

                switch (step.StepType)
                {
                    case "SingleRequest":
                        await ExecuteSingleRequest(savedRequest, result, cancellationToken);
                        break;

                    case "DelayedRequest":
                        await ExecuteDelayedRequest(savedRequest, step, result, cancellationToken);
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

        private async Task ExecuteSingleRequest(SavedRequest savedRequest, TestResult result, CancellationToken cancellationToken)
        {
            result.RequestSent = $"{savedRequest.IpAddress}:{savedRequest.Port}";
            
            var udpRequest = CreateUdpRequestFromSaved(savedRequest);

            var response = await _udpClientService.SendRequestAsync(udpRequest);
            result.ResponseReceived = response != null ? System.Text.Encoding.UTF8.GetString(response.RawData) : "응답 없음";
        }

        private async Task ExecuteDelayedRequest(SavedRequest savedRequest, TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            // Wait for delay first
            if (step.DelaySeconds > 0)
            {
                result.RequestSent = $"{savedRequest.IpAddress}:{savedRequest.Port} (지연 시간: {step.DelaySeconds}초)";
                await Task.Delay(TimeSpan.FromSeconds(step.DelaySeconds), cancellationToken);
            }

            // Execute request after delay
            await ExecuteSingleRequest(savedRequest, result, cancellationToken);
        }

        private async Task ExecutePeriodicRequest(SavedRequest savedRequest, TestStep step, TestResult result, CancellationToken cancellationToken)
        {
            var intervalMs = (int)(1000.0 / step.FrequencyHz); // 밀리초 단위 간격
            var totalExecutions = (int)(step.FrequencyHz * step.DurationSeconds); // 총 실행 횟수 계산
            
            var responses = new System.Text.StringBuilder();
            int executionCount = 0;
            var startTime = DateTime.Now;

            for (int i = 0; i < totalExecutions && !cancellationToken.IsCancellationRequested; i++)
            {
                // 다음 실행 시간 계산
                var nextExecutionTime = startTime.AddMilliseconds((i + 1) * intervalMs);
                
                // 현재 시간이 다음 실행 시간보다 이르면 대기
                var currentTime = DateTime.Now;
                if (currentTime < nextExecutionTime)
                {
                    var waitTime = nextExecutionTime - currentTime;
                    if (waitTime.TotalMilliseconds > 0)
                    {
                        await Task.Delay(waitTime, cancellationToken);
                    }
                }

                // 요청 실행
                var udpRequest = CreateUdpRequestFromSaved(savedRequest);
                var response = await _udpClientService.SendRequestAsync(udpRequest);
                executionCount++;

                responses.AppendLine($"실행 #{executionCount} ({DateTime.Now:HH:mm:ss.fff}): {(response != null ? System.Text.Encoding.UTF8.GetString(response.RawData) : "응답 없음")}");
                
                // 진행 상황 보고 (선택적)
                if (executionCount % Math.Max(1, totalExecutions / 10) == 0)
                {
                    ReportProgress($"주기적 요청 진행 중: {executionCount}/{totalExecutions}", 0);
                }
            }

            result.RequestSent = $"{savedRequest.IpAddress}:{savedRequest.Port} (주기적 실행 {executionCount}회, {step.FrequencyHz}Hz × {step.DurationSeconds}초)";
            result.ResponseReceived = responses.ToString();
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