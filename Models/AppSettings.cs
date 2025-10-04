using System.ComponentModel;

namespace MMG.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private int _customPort = 8080;
        private bool _useCustomPort = true;

        public int CustomPort
        {
            get => _customPort;
            set
            {
                _customPort = value;
                OnPropertyChanged(nameof(CustomPort));
            }
        }

        public bool UseCustomPort
        {
            get => _useCustomPort;
            set
            {
                _useCustomPort = value;
                OnPropertyChanged(nameof(UseCustomPort));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}