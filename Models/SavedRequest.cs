using CommunityToolkit.Mvvm.ComponentModel;

namespace MMG.Models
{
    public partial class SavedRequest : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name = "";

        [ObservableProperty]
        private string ipAddress = "";

        [ObservableProperty]
        private int port;

        [ObservableProperty]
        private bool isBigEndian = true;

        [ObservableProperty]
        private int? folderId;

        [ObservableProperty]
        private string requestSchemaJson = "";

        [ObservableProperty]
        private string responseSchemaJson = "";

        [ObservableProperty]
        private DateTime createdAt;

        [ObservableProperty]
        private DateTime lastModified;

        public SavedRequest()
        {
            CreatedAt = DateTime.Now;
            LastModified = DateTime.Now;
        }
    }
}