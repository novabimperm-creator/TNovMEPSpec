using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TNovMEPSpec
{
    /// <summary>Комплект *.СО в окне «Спеку на листы».</summary>
    public class MEPSpecSheetsSetItem : INotifyPropertyChanged
    {
        private bool _isSelected = true; public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public string SheetSet { get; set; }
        public string ScheduleName { get; set; }
        public string Info { get; set; }
        private string _firstHeight; public string FirstHeight { get => _firstHeight; set { _firstHeight = value; OnPropertyChanged(); } }
        private string _otherHeight; public string OtherHeight { get => _otherHeight; set { _otherHeight = value; OnPropertyChanged(); } }

        internal MEPSpecSheetsSet Data { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }

    public class MEPSpecSheetsViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MEPSpecSheetsSetItem> Sets { get; } = new ObservableCollection<MEPSpecSheetsSetItem>();

        private bool _deleteExtraSheets = true; public bool DeleteExtraSheets { get => _deleteExtraSheets; set { _deleteExtraSheets = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
