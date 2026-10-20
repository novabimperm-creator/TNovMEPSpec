using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovMEPSpec
{
    /// <summary>Мини-окно добавления позиции каталога: группирование (через запятую — несколько элементов) и количество.</summary>
    public partial class MEPSpecNonModelAddWPF : Window
    {
        public List<string> Groupings { get; private set; } = new List<string>();
        public string Quantity { get; private set; } = "1";

        public MEPSpecNonModelAddWPF(string naim)
        {
            InitializeComponent();
            naimText.Text = naim;
            Loaded += (s, e) => groupingBox.Focus();
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (!NonModelElementService.TryParseNumber(quantityBox.Text, out _))
            {
                new InfoWindow280("ADSK_Количество должно быть числом.") { Owner = this }.ShowDialog();
                return;
            }
            Groupings = MEPSpecNonModelCatalogViewModel.SplitGroupings(groupingBox.Text);
            Quantity = quantityBox.Text.Trim();
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
