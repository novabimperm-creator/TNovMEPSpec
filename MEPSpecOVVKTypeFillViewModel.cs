using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TNovMEPSpec
{
    public class MEPSpecOVVKTypeFillViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<OVVKTypeFillRow> Rows { get; }
        public bool CanApply => Rows.All(r => !r.HasError);

        public MEPSpecOVVKTypeFillViewModel(IList<OVVKTypeFillRow> rows)
        {
            Rows = new ObservableCollection<OVVKTypeFillRow>(rows ?? new List<OVVKTypeFillRow>());
            foreach (var r in Rows)
                r.PropertyChanged += (s, e) => OnPropertyChanged(nameof(CanApply));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class OVVKTypeFillRow : INotifyPropertyChanged
    {
        public ElementId TypeId { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }

        private string _typeMark = "";
        public string TypeMark
        {
            get => _typeMark;
            set { _typeMark = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(MarkError)); OnPropertyChanged(nameof(HasError)); }
        }

        private string _typeComments = "";
        public string TypeComments
        {
            get => _typeComments;
            set { _typeComments = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(CommentsError)); OnPropertyChanged(nameof(HasError)); }
        }

        public bool MarkError => string.IsNullOrWhiteSpace(TypeMark);
        public bool CommentsError => string.IsNullOrWhiteSpace(TypeComments);
        public bool HasError => MarkError || CommentsError;

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
