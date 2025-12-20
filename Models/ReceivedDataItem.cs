using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    public partial class ReceivedDataItem : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TimeString))]
        private DateTime timestamp;

        [ObservableProperty]
        private string ipAddress = string.Empty;

        [ObservableProperty]
        private int port;

        [ObservableProperty]
        private string data = string.Empty;

        public string TimeString => Timestamp.ToString("HH:mm:ss.fff");
    }
}