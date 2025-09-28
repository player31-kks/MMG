using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MMG.Models
{
    public class TestScenario : INotifyPropertyChanged
    {
        private int _id;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private DateTime _createdAt;
        private DateTime? _lastRunAt;
        private bool _isEnabled = true;
        private bool _isRunning = false;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(nameof(Description)); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(nameof(CreatedAt)); }
        }

        public DateTime? LastRunAt
        {
            get => _lastRunAt;
            set { _lastRunAt = value; OnPropertyChanged(nameof(LastRunAt)); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(nameof(IsRunning)); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => IsRunning ? "실행중" : "준비됨";

        public ObservableCollection<TestStep> Steps { get; } = new ObservableCollection<TestStep>();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}