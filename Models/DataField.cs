using System.ComponentModel;

namespace MMG.Models
{
    public enum DataType
    {
        Byte,
        Int,
        UInt,
        Float
    }

    public class DataField : INotifyPropertyChanged
    {
        private string _name = "";
        private DataType _type = DataType.Byte;
        private string _value = "";

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }

        public DataType Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
