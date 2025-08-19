using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MMG.Models
{
    public class ResponseSchema : INotifyPropertyChanged
    {
        private ObservableCollection<DataField> _headers = new();
        private ObservableCollection<DataField> _payload = new();

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

        // Backward compatibility - combines Headers and Payload
        public ObservableCollection<DataField> Fields
        {
            get
            {
                var combined = new ObservableCollection<DataField>();
                foreach (var header in Headers)
                    combined.Add(header);
                foreach (var payload in Payload)
                    combined.Add(payload);
                return combined;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class UdpResponse
    {
        public byte[] RawData { get; set; } = Array.Empty<byte>();
        public Dictionary<string, object> ParsedData { get; set; } = new();
        public DateTime ReceivedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "";
    }
}
