using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    public partial class UdpRequest : ObservableObject
    {
        [ObservableProperty]
        private string ipAddress = "127.0.0.1";

        [ObservableProperty]
        private int port = 8080;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EndianText))]
        private bool isBigEndian = true;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(WaitForResponseText))]
        private bool waitForResponse = true;

        [ObservableProperty]
        private ObservableCollection<DataField> headers = new();

        [ObservableProperty]
        private ObservableCollection<DataField> payload = new();

        public string EndianText => IsBigEndian ? "Big Endian" : "Little Endian";
        public string WaitForResponseText => WaitForResponse ? "Wait Response" : "No Wait";
    }
}
