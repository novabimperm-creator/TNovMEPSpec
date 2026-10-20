using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TNovCommon;

namespace TNovMEPSpec
{
    public partial class MEPSpecNonModelWPF : Window
    {
        private readonly MEPSpecNonModelViewModel _vm;

        public MEPSpecNonModelWPF(MEPSpecNonModelViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = viewModel;
        }

        private bool CommitGrid()
        {
            return grid.CommitEdit(DataGridEditingUnit.Row, true);
        }

        //false — сохранение не удалось, окно не закрываем
        private bool TrySave()
        {
            CommitGrid();
            var invalid = _vm.InvalidRows();
            if (invalid.Count > 0)
            {
                new InfoWindow280("ADSK_Количество должно быть числом:\n" + string.Join("\n", invalid.Select(r => r.ADSK_Наименование + ": " + r.ADSK_Количество))) { Owner = this }.ShowDialog();
                return false;
            }
            try
            {
                _vm.SaveEdits();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("Немоделируемые: ошибка сохранения: " + ex.Message, 4);
                new InfoWindow280("Ошибка: " + ex.Message) { Owner = this }.ShowDialog();
                return false;
            }
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            TrySave();
        }

        private void catalogButton_Click(object sender, RoutedEventArgs e)
        {
            //несохранённые правки сначала записываем: после добавления таблица перечитывается из модели
            if (!TrySave()) return;
            NonModelCatalogService service;
            try { service = new NonModelCatalogService(); }
            catch (Exception ex) { new InfoWindow280(ex.Message) { Owner = this }.ShowDialog(); return; }
            var catalogVm = new MEPSpecNonModelCatalogViewModel(RevitAPI.Document, service);
            new MEPSpecNonModelCatalogWPF(catalogVm) { Owner = this }.ShowDialog();
            if (catalogVm.ModelChanged)
            {
                _vm.MarkModelChanged();
                _vm.Reload();
            }
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            CommitGrid();
            if (_vm.HasEdits)
            {
                var ask = MessageBox.Show(this, "Сохранить изменения в модель?", "Немоделируемые", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (ask == MessageBoxResult.Cancel) return;
                if (ask == MessageBoxResult.Yes && !TrySave()) return;
            }
            Close();
        }

        private void Rule_Click(object sender, RoutedEventArgs e)
        {
            new InfoWindow280((sender as System.Windows.Documents.Hyperlink)?.Tag as string ?? "") { Owner = this }.ShowDialog();
            e.Handled = true;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
