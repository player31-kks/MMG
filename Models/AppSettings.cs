using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    public partial class AppSettings : ObservableObject
    {
        [ObservableProperty]
        private int customPort = 8080;

        [ObservableProperty]
        private bool useCustomPort = true;
    }
}