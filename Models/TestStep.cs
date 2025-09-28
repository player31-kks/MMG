using System;
using System.ComponentModel;

namespace MMG.Models
{
    public class TestStep : INotifyPropertyChanged
    {
        private int _id;
        private int _scenarioId;
        private string _name = string.Empty;
        private string _stepType = "SingleRequest";
        private int _savedRequestId;
        private double _delaySeconds = 0;
        private double _frequencyHz = 1.0;
        private int _durationSeconds = 10;
        private string _expectedResponse = string.Empty;
        private bool _isEnabled = true;
        private int _order;

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

        public string StepType
        {
            get => _stepType;
            set { _stepType = value; OnPropertyChanged(nameof(StepType)); }
        }

        public int SavedRequestId
        {
            get => _savedRequestId;
            set { _savedRequestId = value; OnPropertyChanged(nameof(SavedRequestId)); }
        }

        public double DelaySeconds
        {
            get => _delaySeconds;
            set { _delaySeconds = value; OnPropertyChanged(nameof(DelaySeconds)); }
        }

        public double FrequencyHz
        {
            get => _frequencyHz;
            set { _frequencyHz = value; OnPropertyChanged(nameof(FrequencyHz)); }
        }

        public int DurationSeconds
        {
            get => _durationSeconds;
            set { _durationSeconds = value; OnPropertyChanged(nameof(DurationSeconds)); }
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
            set { _order = value; OnPropertyChanged(nameof(Order)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}