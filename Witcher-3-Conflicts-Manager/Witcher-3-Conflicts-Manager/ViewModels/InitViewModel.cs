using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Witcher_3_Conflicts_Manager.Commands;

namespace Witcher_3_Conflicts_Manager.ViewModels
{
    public class InitViewModel : ViewModel
    {
        public InitViewModel()
        {
            NextCommand = new RelayCommand(Next, CanNext);
            LocateTW3Command = new RelayCommand(LocateTW3);
            LocateWCCCommand = new RelayCommand(LocateWCC);

            WCC_Path = Properties.Settings.Default.WCC_Path;
            TW3_Path = Properties.Settings.Default.TW3_Path;
        }

        #region Properties
        public MainViewModel ParentViewModel { get;set; }
        private string _wcc_Path;
        public string WCC_Path
        {
            get => _wcc_Path;
            set
            {
                if (_wcc_Path != value)
                {
                    _wcc_Path = value;
                    OnPropertyChanged();

                    Properties.Settings.Default.WCC_Path = value;
                    Properties.Settings.Default.Save();
                }
            }
        }
        private string _tw3_Path;
        public string TW3_Path
        {
            get => _tw3_Path;
            set
            {
                if (_tw3_Path != value)
                {
                    _tw3_Path = value;
                    OnPropertyChanged();

                    Properties.Settings.Default.TW3_Path = value;
                    Properties.Settings.Default.Save();
                }
            }
        }
        #endregion


        #region Commands
        public ICommand NextCommand { get; }
        public ICommand LocateTW3Command { get; }
        public ICommand LocateWCCCommand { get; }



        #region Command Implementation



        private bool CanNext()
        {
            /*return !String.IsNullOrEmpty(Properties.Settings.Default.WCC_Path) &&
                !String.IsNullOrEmpty(Properties.Settings.Default.TW3_Path) &&
                File.Exists(Properties.Settings.Default.WCC_Path) &&
                File.Exists(Properties.Settings.Default.TW3_Path);*/
            return !String.IsNullOrEmpty(Properties.Settings.Default.TW3_Path) &&
            File.Exists(Properties.Settings.Default.TW3_Path);
        }
        private void Next()
        {
            Properties.Settings.Default.Save();
            ParentViewModel.ShowConflicts(true);
        }

        public void LocateTW3()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.FileName = Properties.Settings.Default.TW3_Path; 
            dlg.DefaultExt = "witcher3.exe"; 
            dlg.Filter = "Witcher 3 Executable (witcher3.exe)|witcher3.exe"; // Filter files by extension

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                TW3_Path = dlg.FileName;
            }


        }
        public void LocateWCC()
        {

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.FileName = Properties.Settings.Default.WCC_Path;
            dlg.DefaultExt = "wcc_lite.exe";
            dlg.Filter = "Witcher 3 Modkit (wcc_lite.exe)|wcc_lite.exe"; // Filter files by extension

            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                WCC_Path = dlg.FileName;
            }

        }


        #endregion
        #endregion

    }
}
