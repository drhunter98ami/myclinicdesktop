using System.ComponentModel;

namespace MyClinic.Models
{
    public class TreatmentSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _quantity;

        public int TreatmentId { get; set; }
        public string TreatmentName { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public string Currency { get; set; } = "SYP";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SelectedTreatment
    {
        public int TreatmentId { get; set; }
        public string TreatmentName { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public string Currency { get; set; } = "SYP";
        public int Quantity { get; set; }
    }
}
