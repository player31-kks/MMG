using System.ComponentModel;

namespace MMG.Models
{
    public class SavedRequest : INotifyPropertyChanged
    {
        private int _id;
        private string _name = "";
        private string _ipAddress = "";
        private int _port;
        private string _requestSchemaJson = "";
        private string _responseSchemaJson = "";
        private DateTime _createdAt;
        private DateTime _lastModified;

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

        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                _ipAddress = value;
                OnPropertyChanged(nameof(IpAddress));
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                _port = value;
                OnPropertyChanged(nameof(Port));
            }
        }

        public string RequestSchemaJson
        {
            get => _requestSchemaJson;
            set
            {
                _requestSchemaJson = value;
                OnPropertyChanged(nameof(RequestSchemaJson));
            }
        }

        public string ResponseSchemaJson
        {
            get => _responseSchemaJson;
            set
            {
                _responseSchemaJson = value;
                OnPropertyChanged(nameof(ResponseSchemaJson));
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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SavedRequest()
        {
            CreatedAt = DateTime.Now;
            LastModified = DateTime.Now;
        }
    }
}