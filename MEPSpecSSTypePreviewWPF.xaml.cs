using System.Windows;
using System.Windows.Input;

namespace TNovMEPSpec
{
    public partial class MEPSpecSSTypePreviewWPF : Window
    {
        public MEPSpecSSTypePreviewWPF(MEPSpecSSTypePreviewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequest += (s, e) => Close();
            viewModel.ContinueRequest += (s, e) => Close();
            Closed += (s, e) => viewModel.OnWindowClosed();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
