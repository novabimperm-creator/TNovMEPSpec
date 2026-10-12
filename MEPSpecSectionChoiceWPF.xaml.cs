using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TNovMEPSpec
{
    public partial class MEPSpecSectionChoiceWPF : Window
    {
        /// <summary>Выбранный раздел: "vkov", "el", "ss" или null при отмене.</summary>
        public string SelectedSection { get; private set; }

        public MEPSpecSectionChoiceWPF()
        {
            InitializeComponent();
        }

        private void sectionButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedSection = (string)((Button)sender).Tag;
            DialogResult = true;
            Close();
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
