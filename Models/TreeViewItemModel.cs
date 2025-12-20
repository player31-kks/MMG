using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MMG.Models
{
    public enum TreeViewItemType
    {
        Folder,
        Request
    }

    public partial class TreeViewItemModel : ObservableObject
    {
        [ObservableProperty]
        private string name = "";

        [ObservableProperty]
        private TreeViewItemType itemType;

        [ObservableProperty]
        private bool isExpanded = true;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool isEditing;

        [ObservableProperty]
        private ObservableCollection<TreeViewItemModel> children = new();

        [ObservableProperty]
        private object? tag; // Folder 또는 SavedRequest 객체 저장
    }
}