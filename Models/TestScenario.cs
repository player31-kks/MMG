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

        /// <summary>
        /// 시나리오 레벨 바인딩 포트 (0이면 자동)
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BindPortText))]
        private int bindPort = 0;

        /// <summary>
        /// 바인딩 포트 사용 여부
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BindPortText))]
        private bool useBindPort = false;

        public string StatusText => IsRunning ? "실행중" : "준비됨";

        public string BindPortText => UseBindPort 
            ? (BindPort > 0 ? $"Port {BindPort}" : "자동 할당") 
            : "사용 안함";

        public ObservableCollection<TestStep> Steps { get; } = new ObservableCollection<TestStep>();

        // 호환성을 위한 별칭
        public ObservableCollection<TestStep> TestSteps => Steps;
    }
}