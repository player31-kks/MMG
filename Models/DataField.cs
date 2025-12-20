using CommunityToolkit.Mvvm.ComponentModel;

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

    public partial class DataField : ObservableObject
    {
        [ObservableProperty]
        private string name = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPadding))]
        [NotifyPropertyChangedFor(nameof(Value))]
        private DataType type = DataType.Byte;

        [ObservableProperty]
        private string valueString = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Value))]
        private int paddingSize = 1;

        public string Value
        {
            get => Type == DataType.Padding ? PaddingSize.ToString() : ValueString;
            set
            {
                if (Type == DataType.Padding)
                {
                    if (int.TryParse(value, out int paddingValue))
                    {
                        PaddingSize = paddingValue;
                    }
                }
                else
                {
                    ValueString = value;
                }
                OnPropertyChanged(nameof(Value));
            }
        }

        public bool IsPadding => Type == DataType.Padding;
    }
}
