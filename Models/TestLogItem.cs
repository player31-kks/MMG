using System;
using System.ComponentModel;

namespace MMG.Models
{
    /// <summary>
    /// 로그 레벨
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// 테스트 실행 로그 아이템
    /// </summary>
    public class TestLogItem : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private LogLevel _level;
        private string _message = string.Empty;
        private string _stepName = string.Empty;
        private string _details = string.Empty;

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); OnPropertyChanged(nameof(TimestampText)); }
        }

        public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

        public LogLevel Level
        {
            get => _level;
            set { _level = value; OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(LevelText)); OnPropertyChanged(nameof(LevelColor)); }
        }

        public string LevelText => Level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Success => "SUCCESS",
            _ => "INFO"
        };

        public string LevelColor => Level switch
        {
            LogLevel.Debug => "#6C757D",
            LogLevel.Info => "#17A2B8",
            LogLevel.Warning => "#FFC107",
            LogLevel.Error => "#DC3545",
            LogLevel.Success => "#28A745",
            _ => "#495057"
        };

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(nameof(Message)); }
        }

        public string StepName
        {
            get => _stepName;
            set { _stepName = value; OnPropertyChanged(nameof(StepName)); }
        }

        public string Details
        {
            get => _details;
            set { _details = value; OnPropertyChanged(nameof(Details)); OnPropertyChanged(nameof(HasDetails)); }
        }

        public bool HasDetails => !string.IsNullOrEmpty(Details);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public static TestLogItem Create(LogLevel level, string message, string stepName = "", string details = "")
        {
            return new TestLogItem
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                StepName = stepName,
                Details = details
            };
        }
    }
}
