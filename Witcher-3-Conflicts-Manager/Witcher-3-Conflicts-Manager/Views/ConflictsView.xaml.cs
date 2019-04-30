using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Witcher_3_Conflicts_Manager.Views
{
    /// <summary>
    /// Interaction logic for ConflictsView.xaml
    /// </summary>
    public partial class ConflictsView : UserControl
    {
        public ConflictsView()
        {
            InitializeComponent();
        }


        private void OnCbObjectsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            comboBox.SelectedItem = null;
        }

        private void OnCbObjectCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (object cbObject in _combo.Items)
            {
                ((ViewModels.ConflictsViewModel)DataContext).ReloadConflictsList();
            }
            
        }
    }
}
