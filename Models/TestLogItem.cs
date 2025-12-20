using System;
using CommunityToolkit.Mvvm.ComponentModel;

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
    public partial class TestLogItem : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TimestampText))]
        private DateTime timestamp;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LevelText))]
        [NotifyPropertyChangedFor(nameof(LevelColor))]
        private LogLevel level;

        [ObservableProperty]
        private string message = string.Empty;

        [ObservableProperty]
        private string stepName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasDetails))]
        private string details = string.Empty;

        public string TimestampText => Timestamp.ToString("HH:mm:ss.fff");

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

        public bool HasDetails => !string.IsNullOrEmpty(Details);

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
