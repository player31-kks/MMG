using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    /// <summary>
    /// 테스트 스텝 타입
    /// </summary>
    public enum StepExecutionType
    {
        /// <summary>즉시 실행 (지연 없음)</summary>
        Immediate,
        /// <summary>실행 전 지연</summary>
        PreDelayed,
        /// <summary>실행 후 지연</summary>
        PostDelayed,
        /// <summary>주기적 실행</summary>
        Periodic,
        /// <summary>시나리오 시작 기준 절대 지연</summary>
        AbsoluteDelayed,
        /// <summary>메시지 수신 대기 (서버 모드)</summary>
        WaitForMessage,
        /// <summary>수신 후 응답 (서버 모드)</summary>
        ReceiveAndReply
    }

    /// <summary>
    /// 스텝 실행 모드
    /// </summary>
    public enum StepRunMode
    {
        /// <summary>순차 실행 - 이전 스텝 완료 후 실행</summary>
        Sequential,
        /// <summary>백그라운드 실행 - 다음 스텝과 동시 실행</summary>
        Background
    }

    public partial class TestStep : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private int scenarioId;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StepTypeDisplay))]
        private string stepType = "Immediate";

        [ObservableProperty]
        private int savedRequestId;

        [ObservableProperty]
        private int preDelayMs = 0;

        [ObservableProperty]
        private int postDelayMs = 0;

        [ObservableProperty]
        private int intervalMs = 100;

        [ObservableProperty]
        private double frequencyHz = 1.0;

        [ObservableProperty]
        private int durationMs = 1000;

        [ObservableProperty]
        private int repeatCount = 1;

        [ObservableProperty]
        private string expectedResponse = string.Empty;

        [ObservableProperty]
        private bool isEnabled = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StepNumber))]
        private int order;

        [ObservableProperty]
        private bool isRunning = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasResult))]
        private TestResult? lastResult;

        [ObservableProperty]
        private bool hasFailed = false;

        [ObservableProperty]
        private string lastErrorMessage = string.Empty;

        [ObservableProperty]
        private string statusText = "대기";

        // ========== 새로운 속성들 (백그라운드 실행 및 시나리오 기준 지연) ==========

        /// <summary>
        /// 백그라운드에서 실행할지 여부 (true면 다음 스텝과 동시 실행)
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RunModeDisplay))]
        private bool isBackground = false;

        /// <summary>
        /// 시나리오 시작 시점부터의 절대 지연 시간 (ms)
        /// StepType이 AbsoluteDelayed일 때 사용
        /// </summary>
        [ObservableProperty]
        private int startDelayFromScenarioMs = 0;

        /// <summary>
        /// 서버 모드에서 수신 대기할 로컬 포트
        /// </summary>
        [ObservableProperty]
        private int listenPort = 0;

        /// <summary>
        /// 서버 모드에서 수신 대기 타임아웃 (ms)
        /// </summary>
        [ObservableProperty]
        private int receiveTimeoutMs = 5000;

        /// <summary>
        /// 응답으로 보낼 SavedRequest ID (ReceiveAndReply 모드에서 사용)
        /// </summary>
        [ObservableProperty]
        private int responseRequestId = 0;

        // ========== Display 속성들 ==========

        public string StepTypeDisplay => StepType switch
        {
            "Immediate" => "즉시 실행",
            "PreDelayed" => "실행 전 지연",
            "PostDelayed" => "실행 후 지연",
            "Periodic" => "주기적 실행",
            "AbsoluteDelayed" => "절대 지연",
            "WaitForMessage" => "수신 대기",
            "ReceiveAndReply" => "수신 후 응답",
            _ => StepType
        };

        public string RunModeDisplay => IsBackground ? "백그라운드" : "순차";

        // ========== 하위 호환성을 위한 속성들 ==========

        public double DelaySeconds
        {
            get => PreDelayMs / 1000.0;
            set => PreDelayMs = (int)(value * 1000);
        }

        public int DurationSeconds
        {
            get => DurationMs / 1000;
            set => DurationMs = value * 1000;
        }

        public bool HasResult => LastResult != null;

        public int StepNumber => Order;

        public SavedRequest? Request { get; set; }
    }
}