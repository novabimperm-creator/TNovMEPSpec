using System.Windows;
using System.Windows.Input;

namespace TNovMEPSpec
{
    public partial class MEPSpecSSPreflightWPF : Window
    {
        private readonly MEPSpecWindowCollapse _collapse;

        public MEPSpecSSPreflightWPF(MEPSpecSSPreflightViewModel viewModel)
        {
            InitializeComponent();
            _collapse = new MEPSpecWindowCollapse(this, RootGrid, TitleColumn, MinimizeButton, CollapseButton, CloseButton);
            DataContext = viewModel;
            viewModel.CloseRequest += (s, e) => Close();
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e) => _collapse.OnSourceInitialized();
        private void Window_MouseEnter(object sender, MouseEventArgs e) => _collapse.OnMouseEnter();
        private void Window_MouseLeave(object sender, MouseEventArgs e) => _collapse.OnMouseLeave();
        private void Minimize_Click(object sender, RoutedEventArgs e) => _collapse.Minimize();
        private void CollapseButton_Click(object sender, RoutedEventArgs e) => _collapse.Toggle();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
