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
        Periodic
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

        public string StepTypeDisplay => StepType switch
        {
            "Immediate" => "즉시 실행",
            "PreDelayed" => "실행 전 지연",
            "PostDelayed" => "실행 후 지연",
            "Periodic" => "주기적 실행",
            _ => StepType
        };

        // 하위 호환성을 위한 속성들
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