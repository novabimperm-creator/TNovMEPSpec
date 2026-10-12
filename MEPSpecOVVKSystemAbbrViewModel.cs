using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TNovMEPSpec
{
    public class MEPSpecOVVKSystemAbbrViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<OVVKSystemAbbrRow> Rows { get; }
        public bool CanApply => Rows.All(r => !r.HasError);

        public MEPSpecOVVKSystemAbbrViewModel(IList<OVVKSystemAbbrRow> rows)
        {
            Rows = new ObservableCollection<OVVKSystemAbbrRow>(rows ?? new List<OVVKSystemAbbrRow>());
            foreach (var r in Rows)
                r.PropertyChanged += (s, e) => OnPropertyChanged(nameof(CanApply));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class OVVKSystemAbbrRow : INotifyPropertyChanged
    {
        public ElementId TypeId { get; set; }
        public string SystemClass { get; set; }
        public string TypeName { get; set; }

        private string _abbreviation = "";
        public string Abbreviation
        {
            get => _abbreviation;
            set { _abbreviation = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        //пустое сокращение тоже не принимаем
        public bool HasError => string.IsNullOrWhiteSpace(Abbreviation) || MEPSpecTools.HasLatin(Abbreviation);

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
