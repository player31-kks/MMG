using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MMG.Models
{
    public partial class Folder : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name = "";

        [ObservableProperty]
        private int? parentId;

        [ObservableProperty]
        private DateTime createdAt;

        [ObservableProperty]
        private DateTime lastModified;

        [ObservableProperty]
        private bool isExpanded = true;

        [ObservableProperty]
        private ObservableCollection<Folder> subFolders = new();

        [ObservableProperty]
        private ObservableCollection<SavedRequest> requests = new();

        public Folder()
        {
            CreatedAt = DateTime.Now;
            LastModified = DateTime.Now;
        }
    }
}