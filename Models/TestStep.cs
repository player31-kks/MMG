using System;
using System.ComponentModel;

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

    public class TestStep : INotifyPropertyChanged
    {
        private int _id;
        private int _scenarioId;
        private string _name = string.Empty;
        private string _stepType = "Immediate";
        private int _savedRequestId;
        private int _preDelayMs = 0;
        private int _postDelayMs = 0;
        private int _intervalMs = 100;
        private double _frequencyHz = 1.0;
        private int _durationMs = 1000;
        private int _repeatCount = 1;
        private string _expectedResponse = string.Empty;
        private bool _isEnabled = true;
        private int _order;
        private bool _isRunning = false;
        private TestResult? _lastResult;
        private bool _hasFailed = false;
        private string _lastErrorMessage = string.Empty;
        private string _statusText = "대기";

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public int ScenarioId
        {
            get => _scenarioId;
            set { _scenarioId = value; OnPropertyChanged(nameof(ScenarioId)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        /// <summary>
        /// 스텝 타입: Immediate, PreDelayed, PostDelayed, Periodic
        /// </summary>
        public string StepType
        {
            get => _stepType;
            set { _stepType = value; OnPropertyChanged(nameof(StepType)); OnPropertyChanged(nameof(StepTypeDisplay)); }
        }

        public string StepTypeDisplay => StepType switch
        {
            "Immediate" => "즉시 실행",
            "PreDelayed" => "실행 전 지연",
            "PostDelayed" => "실행 후 지연",
            "Periodic" => "주기적 실행",
            _ => StepType
        };

        public int SavedRequestId
        {
            get => _savedRequestId;
            set { _savedRequestId = value; OnPropertyChanged(nameof(SavedRequestId)); }
        }

        /// <summary>실행 전 지연 시간 (ms)</summary>
        public int PreDelayMs
        {
            get => _preDelayMs;
            set { _preDelayMs = value; OnPropertyChanged(nameof(PreDelayMs)); }
        }

        /// <summary>실행 후 지연 시간 (ms)</summary>
        public int PostDelayMs
        {
            get => _postDelayMs;
            set { _postDelayMs = value; OnPropertyChanged(nameof(PostDelayMs)); }
        }

        /// <summary>주기적 실행 주파수 (Hz)</summary>
        public double FrequencyHz
        {
            get => _frequencyHz;
            set { _frequencyHz = value; OnPropertyChanged(nameof(FrequencyHz)); }
        }

        /// <summary>주기적 실행 간격 (ms)</summary>
        public int IntervalMs
        {
            get => _intervalMs;
            set { _intervalMs = value; OnPropertyChanged(nameof(IntervalMs)); }
        }

        /// <summary>주기적 실행 지속 시간 (ms)</summary>
        public int DurationMs
        {
            get => _durationMs;
            set { _durationMs = value; OnPropertyChanged(nameof(DurationMs)); }
        }

        /// <summary>반복 횟수 (Periodic에서 사용)</summary>
        public int RepeatCount
        {
            get => _repeatCount;
            set { _repeatCount = value; OnPropertyChanged(nameof(RepeatCount)); }
        }

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

        public string ExpectedResponse
        {
            get => _expectedResponse;
            set { _expectedResponse = value; OnPropertyChanged(nameof(ExpectedResponse)); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(nameof(Order)); OnPropertyChanged(nameof(StepNumber)); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(nameof(IsRunning)); }
        }

        public TestResult? LastResult
        {
            get => _lastResult;
            set { _lastResult = value; OnPropertyChanged(nameof(LastResult)); OnPropertyChanged(nameof(HasResult)); }
        }

        public bool HasResult => LastResult != null;

        public bool HasFailed
        {
            get => _hasFailed;
            set { _hasFailed = value; OnPropertyChanged(nameof(HasFailed)); }
        }

        public string LastErrorMessage
        {
            get => _lastErrorMessage;
            set { _lastErrorMessage = value; OnPropertyChanged(nameof(LastErrorMessage)); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public int StepNumber => Order;

        public SavedRequest? Request { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}