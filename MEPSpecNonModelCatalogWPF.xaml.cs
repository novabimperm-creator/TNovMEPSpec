using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TNovCommon;

namespace TNovMEPSpec
{
    public partial class MEPSpecNonModelCatalogWPF : Window
    {
        private readonly MEPSpecNonModelCatalogViewModel _vm;

        public MEPSpecNonModelCatalogWPF(MEPSpecNonModelCatalogViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = viewModel;
        }

        private void grid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit) _vm.CatalogEdited = true;
        }

        private void addButton_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as FrameworkElement)?.DataContext as NonModelCatalogRow;
            if (row == null) return;
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            var ask = new MEPSpecNonModelAddWPF(row.ADSK_Наименование) { Owner = this };
            if (ask.ShowDialog() != true) return;
            try
            {
                int created = _vm.AddToModel(row, ask.Groupings, ask.Quantity);
                new InfoWindow280("Добавлено элементов: " + created) { Owner = this }.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Log("Немоделируемые: ошибка добавления из каталога: " + ex.Message, 4);
                new InfoWindow280("Ошибка: " + ex.Message) { Owner = this }.ShowDialog();
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            if (_vm.CatalogEdited)
            {
                try { _vm.SaveCatalog(); }
                catch (Exception ex)
                {
                    Logger.Log("Каталог немоделируемых не сохранён: " + ex.Message, 4);
                    new InfoWindow280("Каталог не сохранён (" + _vm.CatalogPath + "): " + ex.Message) { Owner = this }.ShowDialog();
                }
            }
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }

    //кнопка «Добавить» только у настоящих строк каталога (не у пустой строки для новой позиции)
    public class CatalogRowVisibilityConverter : IValueConverter
    {
        public static readonly CatalogRowVisibilityConverter Instance = new CatalogRowVisibilityConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is NonModelCatalogRow ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
