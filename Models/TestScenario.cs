using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    public partial class TestScenario : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private DateTime createdAt;

        [ObservableProperty]
        private DateTime? lastRunAt;

        [ObservableProperty]
        private bool isEnabled = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusText))]
        private bool isRunning = false;

        [ObservableProperty]
        private bool isEditing = false;

        [ObservableProperty]
        private double progress = 0;

        public string StatusText => IsRunning ? "실행중" : "준비됨";

        public ObservableCollection<TestStep> Steps { get; } = new ObservableCollection<TestStep>();

        // 호환성을 위한 별칭
        public ObservableCollection<TestStep> TestSteps => Steps;
    }
}