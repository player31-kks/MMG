using System.ComponentModel;

namespace MMG.ViewModels
{
    public class TestsViewModel : INotifyPropertyChanged
    {
        private string _testName = "";
        private string _testDescription = "";
        private bool _isTestRunning = false;

        public TestsViewModel()
        {
            // 테스트 관련 초기화
        }

        public string TestName
        {
            get => _testName;
            set
            {
                if (_testName != value)
                {
                    _testName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TestDescription
        {
            get => _testDescription;
            set
            {
                if (_testDescription != value)
                {
                    _testDescription = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsTestRunning
        {
            get => _isTestRunning;
            set
            {
                if (_isTestRunning != value)
                {
                    _isTestRunning = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}