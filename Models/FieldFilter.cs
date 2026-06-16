using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    public partial class FieldFilter : ObservableObject
    {
        [ObservableProperty] private int offset = 0;
        [ObservableProperty] private string type = "Byte";
        [ObservableProperty] private string value = "0";
    }
}
