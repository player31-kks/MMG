using System.ComponentModel;
using System.Collections.ObjectModel;

namespace MMG.Models
{
    public class Folder : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private int? _parentId;
        private DateTime _createdAt;
        private DateTime _lastModified;
        private bool _isExpanded = true;
        private ObservableCollection<Folder> _subFolders = new();
        private ObservableCollection<SavedRequest> _requests = new();

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public int? ParentId
        {
            get => _parentId;
            set
            {
                _parentId = value;
                OnPropertyChanged(nameof(ParentId));
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                _createdAt = value;
                OnPropertyChanged(nameof(CreatedAt));
            }
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set
            {
                _lastModified = value;
                OnPropertyChanged(nameof(LastModified));
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

        public ObservableCollection<Folder> SubFolders
        {
            get => _subFolders;
            set
            {
                _subFolders = value;
                OnPropertyChanged(nameof(SubFolders));
            }
        }

        public ObservableCollection<SavedRequest> Requests
        {
            get => _requests;
            set
            {
                _requests = value;
                OnPropertyChanged(nameof(Requests));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Folder()
        {
            CreatedAt = DateTime.Now;
            LastModified = DateTime.Now;
        }
    }
}