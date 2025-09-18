using System.ComponentModel;

namespace MMG.Models
{
    public enum DataType
    {
        Byte,
        UInt16,
        Int,
        UInt,
        Float,
        Padding
    }

    public class DataField : INotifyPropertyChanged
    {
        private string _name = "";
        private DataType _type = DataType.Byte;
        private string _value = "";
        private int _paddingSize = 1;

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
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged(nameof(Type));
                    OnPropertyChanged(nameof(IsPadding));
                    OnPropertyChanged(nameof(Value)); // Type 변경 시 Value도 업데이트
                }
            }
        }

        public string Value
        {
            get => _type == DataType.Padding ? _paddingSize.ToString() : _value;
            set
            {
                if (_type == DataType.Padding)
                {
                    if (int.TryParse(value, out int paddingValue))
                    {
                        _paddingSize = paddingValue;
                        OnPropertyChanged(nameof(PaddingSize));
                    }
                }
                else
                {
                    _value = value;
                }
                OnPropertyChanged(nameof(Value));
            }
        }

        public int PaddingSize
        {
            get => _paddingSize;
            set
            {
                _paddingSize = value;
                OnPropertyChanged(nameof(PaddingSize));
            }
        }

        public bool IsPadding => Type == DataType.Padding;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
