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
using Microsoft.Win32;
using System.Diagnostics;


namespace Witcher_3_Conflicts_Manager.ViewModels
{
    using Commands;
    using Services;
    using Models;

    public class MainViewModel : ViewModel
    {
        public SettingsViewModel svm { get; set; }
        public ConflictsViewModel cvm { get; set; }
        public FinishedViewModel fvm { get; set; }

        public MainViewModel()
        {
            //ConfigService = configService;
            //Logger = logger;

            svm = new SettingsViewModel
            {
                ParentViewModel = this
            };
            cvm = new ConflictsViewModel
            {
                ParentViewModel = this
            };
            fvm = new FinishedViewModel
            {
                ParentViewModel = this
            };

            //View Manager
            //FIXME
            if (String.IsNullOrEmpty(Properties.Settings.Default.WCC_Path) ||
                String.IsNullOrEmpty(Properties.Settings.Default.TW3_Path) ||
                !File.Exists(Properties.Settings.Default.WCC_Path) ||
                !File.Exists(Properties.Settings.Default.TW3_Path)
                )
                ShowSettings();
            else
                ShowConflicts(false);
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
        //public ICommand ExitCommand { get; }
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
            Content = svm;
        }

        public void ShowConflicts(bool reload)
        {
            Content = cvm;
            if (reload)
                cvm.Reload();
        }

        public void ShowFinished()
        {
            Content = fvm;
        }


        #endregion
    }
}