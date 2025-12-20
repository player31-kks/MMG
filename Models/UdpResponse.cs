using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    public partial class ResponseSchema : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<DataField> headers = new();

        [ObservableProperty]
        private ObservableCollection<DataField> payload = new();

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
    }

    public class UdpResponse
    {
        public byte[] RawData { get; set; } = Array.Empty<byte>();
        public Dictionary<string, object> ParsedData { get; set; } = new();
        public DateTime ReceivedAt { get; set; } = DateTime.Now;
        public string Status { get; set; } = "";
    }
}
