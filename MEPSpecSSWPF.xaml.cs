using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovMEPSpec
{
    /// <summary>
    /// Логика взаимодействия для MEPSpecSSWPF.xaml
    /// </summary>
    public partial class MEPSpecSSWPF : Window
    {
        public MEPSpecSSWPF(MEPSpecSSViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            SizeToContent = SizeToContent.Height;
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
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

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = HelpLinks.GetHelpLink("Сводная спека");
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
}
