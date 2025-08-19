using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MMG.Models
{
    public class ResponseSchema : INotifyPropertyChanged
    {
        private ObservableCollection<DataField> _fields = new();

        public ObservableCollection<DataField> Fields
        {
            get => _fields;
            set
            {
                _fields = value;
                OnPropertyChanged(nameof(Fields));
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
