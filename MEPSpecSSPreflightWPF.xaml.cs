using System.Windows;
using System.Windows.Input;

namespace TNovMEPSpec
{
    public partial class MEPSpecSSPreflightWPF : Window
    {
        public MEPSpecSSPreflightWPF(MEPSpecSSPreflightViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequest += (s, e) => Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
