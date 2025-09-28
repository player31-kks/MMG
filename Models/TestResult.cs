using System;

namespace MMG.Models
{
    public class TestResult
    {
        public int Id { get; set; }
        public int ScenarioId { get; set; }
        public int StepId { get; set; }
        public DateTime ExecutedAt { get; set; }
        public bool IsSuccess { get; set; }
        public string RequestSent { get; set; } = string.Empty;
        public string ResponseReceived { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public double ExecutionTimeMs { get; set; }
    }

    public class TestProgressEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public double ProgressPercentage { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class TestCompletedEventArgs : EventArgs
    {
        public int TotalSteps { get; set; }
        public int SuccessfulSteps { get; set; }
        public int FailedSteps { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
        public string Summary { get; set; } = string.Empty;
    }
}