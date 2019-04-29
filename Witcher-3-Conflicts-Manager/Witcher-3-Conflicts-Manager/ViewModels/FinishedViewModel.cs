using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Witcher_3_Conflicts_Manager.Commands;

namespace Witcher_3_Conflicts_Manager.ViewModels
{
    public class FinishedViewModel : ViewModel
    {
        public FinishedViewModel()
        {
            ReloadCommand = new RelayCommand(Reload, CanReload);
        }

        public MainViewModel ParentViewModel { get; set; }

        #region Commands
        public ICommand ReloadCommand { get; }



        #region Command Implementation


        public bool CanReload() => true;

        public void Reload() => ParentViewModel.ShowConflicts(true);

        #endregion
        #endregion
    }



}
