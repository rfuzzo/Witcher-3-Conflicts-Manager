using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Diagnostics;


namespace Witcher_3_Conflicts_Manager.ViewModels
{
    using Commands;
    using Services;
    using Models;

    public class MainViewModel : ViewModel
    {
        public Dictionary<string,ViewModel> ViewModels;

        public MainViewModel()
        {
            ViewModels = new Dictionary<string, ViewModel>();
            ViewModels.Add("init", new InitViewModel { ParentViewModel = this });
            ViewModels.Add("settings", new SettingsViewModel { ParentViewModel = this });
            ViewModels.Add("finished", new FinishedViewModel { ParentViewModel = this });
            ViewModels.Add("conflicts", new ConflictsViewModel { ParentViewModel = this });


            //View Manager
            if (String.IsNullOrEmpty(Properties.Settings.Default.TW3_Path) ||
                !File.Exists(Properties.Settings.Default.TW3_Path)
                )
                ShowInit();
            else
                ShowConflicts(true);
        }

        

        #region Services
        //public IConfigService ConfigService { get; }
        //public ILoggerService Logger { get; }
        #endregion

        #region Properties
        private object _content;
        public object Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged();
                }
            }
        }


        #endregion

        #region Commands
        //public ICommand StartGameCommand { get; }



        #region Command Implementation
        /*


                private bool CanStartGame()
                {
                    return !String.IsNullOrEmpty(Config.GetConfigSetting("TW3_Path"));

                }
                private void StartGame()
                {
                    Process.Start(Config.GetConfigSetting("TW3_Path"));
                }



            */
        #endregion
        #endregion

        #region Methods
        public void ShowSettings()
        {
            var svm = (SettingsViewModel)ViewModels["settings"];
            svm.Reload();
            Content = svm;
        }

        public void ShowFinished() => Content = ViewModels["finished"];
        public void ShowInit() => Content = ViewModels["init"];

        public void ShowConflicts(bool reload)
        {
            var cvm = (ConflictsViewModel)ViewModels["conflicts"];
            if (reload)
                cvm.Refresh();
            Content = cvm;
        }



        #endregion
    }
}