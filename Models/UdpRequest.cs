using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MMG.Models
{
    public class UdpRequest : INotifyPropertyChanged
    {
        private string _ipAddress = "127.0.0.1";
        private int _port = 8080;
        private ObservableCollection<DataField> _headers = new();
        private ObservableCollection<DataField> _payload = new();

        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                _ipAddress = value;
                OnPropertyChanged(nameof(IpAddress));
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
            }
        }

        public ObservableCollection<DataField> Headers
        {
            get => _headers;
            set
            {
                _headers = value;
                OnPropertyChanged(nameof(Headers));
            }
        }

        public ObservableCollection<DataField> Payload
        {
            get => _payload;
            set
            {
                _payload = value;
                OnPropertyChanged(nameof(Payload));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
