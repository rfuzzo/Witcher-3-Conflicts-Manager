using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WolvenKit.Bundles;
using WolvenKit.Common;

namespace Witcher_3_Conflicts_Manager.ViewModels
{
    using Commands;
    using Models;

    public class ConflictsViewModel : ViewModel
    {
        public ConflictsViewModel()
        {
            ConflictsList = new ObservableCollection<BundleConflict>();

            PatchCommand = new RelayCommand(Patch, CanPatch);

            //<baseGame>\bin\x64\witcher3.exe
            BaseGameDir = Path.GetFullPath(Path.Combine(Properties.Settings.Default.TW3_Path, @"..\..\"));
            ModDir = Path.Combine(BaseGameDir, @"Mods");

            //initialize conflicts list
            GetConflictsList();

        }

        

        #region Properties
        public MainViewModel ParentViewModel { get; set; }
        public ObservableCollection<BundleConflict> ConflictsList { get; set; }

       
        private BundleConflict _selectedItem;
        public BundleConflict SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    OnPropertyChanged();
                }
            }
        }

        /*public IEnumerable<IWitcherFileViewModel> Options
        {
            get { return optionsAsViewModels ?? (optionsAsViewModels = new System.Windows.Documents.List(SelectedItem.Items.Select(x => new IWitcherFileViewModel { File = x }))); }
        }
        private IEnumerable<IWitcherFileViewModel> optionsAsViewModels;*/

        private string ModDir { get; set; }
        private string BaseGameDir { get; set; }
        #endregion


        #region Commands
        public ICommand PatchCommand { get; }



        #region Command Implementation
        public bool CanPatch()
        {
            return false;
        }
        public void Patch()
        {


        }


        #endregion
        #endregion

        #region Methods
        private void GetConflictsList()
        {
            //read out all bundles
            List<string> allFiles = new List<string>();
            List<string> allBundles = new List<string>();

            allBundles = Directory.GetFiles(ModDir, "blob0.bundle", SearchOption.AllDirectories).ToList();
            BundleManager bm = new BundleManager();
            foreach (var bundle in allBundles)
                bm.LoadBundle(bundle);

            List<KeyValuePair<string, List<IWitcherFile>>> allConflicts = bm.Items.Where(kvp => kvp.Value.Count > 1).ToList();

            //cast to tw3conflict
            foreach (var c in allConflicts)
            {
                BundleConflict tw3Conflict = new BundleConflict
                {
                    Name = c.Key.Split('\\').Last(),
                    Category = c.Key.Split('\\').First(),
                    Items = c.Value
                };
                ConflictsList.Add(tw3Conflict);
            }




        }


        #endregion
    }
}
