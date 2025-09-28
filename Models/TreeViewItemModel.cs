using System.ComponentModel;
using System.Collections.ObjectModel;

namespace MMG.Models
{
    public enum TreeViewItemType
    {
        Folder,
        Request
    }

    public class TreeViewItemModel : INotifyPropertyChanged
    {
        private string _name = "";
        private TreeViewItemType _itemType;
        private bool _isExpanded = true;
        private bool _isSelected;
        private ObservableCollection<TreeViewItemModel> _children = new();
        private object? _tag; // Folder 또는 SavedRequest 객체 저장

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public TreeViewItemType ItemType
        {
            get => _itemType;
            set
            {
                _itemType = value;
                OnPropertyChanged(nameof(ItemType));
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public ObservableCollection<TreeViewItemModel> Children
        {
            get => _children;
            set
            {
                _children = value;
                OnPropertyChanged(nameof(Children));
            }
        }

        public object? Tag
        {
            get => _tag;
            set
            {
                _tag = value;
                OnPropertyChanged(nameof(Tag));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}