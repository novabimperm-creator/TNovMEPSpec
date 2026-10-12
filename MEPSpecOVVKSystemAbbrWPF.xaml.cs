using System.Windows;
using System.Windows.Input;

namespace TNovMEPSpec
{
    public partial class MEPSpecOVVKSystemAbbrWPF : Window
    {
        public MEPSpecOVVKSystemAbbrWPF(MEPSpecOVVKSystemAbbrViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (!((MEPSpecOVVKSystemAbbrViewModel)DataContext).CanApply) return;
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
