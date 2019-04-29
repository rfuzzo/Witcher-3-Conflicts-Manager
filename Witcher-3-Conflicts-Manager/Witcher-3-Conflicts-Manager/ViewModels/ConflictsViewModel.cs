﻿using System;
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
    
    /// <summary>
    /// 
    /// </summary>
    public class ConflictsViewModel : ViewModel
    {
        private const string modPatchName = "mod0000_PatchedFiles";
        private readonly string[] hiddenExts = new string[] { "xml", "csv", "xbm" };

        public ConflictsViewModel()
        {
            ConflictsList = new ObservableCollection<IConflictWrapper>();

            PatchCommand = new RelayCommand(Patch, CanPatch);
            ShowSettingsCommand = new RelayCommand(ShowSettings, CanShowSettings);

            //<baseGame>\bin\x64\witcher3.exe
            BaseGameDir = Path.GetFullPath(Path.Combine(Properties.Settings.Default.TW3_Path, @"\..\..\")); //Check if this works on your computer.
            ModDir = Path.Combine(BaseGameDir, @"Mods");

            //Load conflicts list
            GetConflictsList();

        }

        public void Reload()
        {
            ConflictsList.Clear();
            GetConflictsList();
        }
        

        #region Properties
        public MainViewModel ParentViewModel { get; set; }
        public ObservableCollection<IConflictWrapper> ConflictsList { get; set; }

       
        private IConflictWrapper _selectedConflict;
        public IConflictWrapper SelectedConflict
        {
            get => _selectedConflict;
            set
            {
                if (_selectedConflict != value)
                {
                    _selectedConflict = value;
                    OnPropertyChanged();
                }
            }
        }

        private string ModDir { get; set; }
        private string BaseGameDir { get; set; }
        #endregion


        #region Commands
        public ICommand PatchCommand { get; }
        public ICommand ShowSettingsCommand { get; }



        #region Command Implementation
        /// <summary>
        /// Returns true if for all conflicts a file has been selected.
        /// </summary>
        /// <returns></returns>
        //public bool CanPatch() => !ConflictsList.Where(x => !x.Resolved()).Any();
        public bool CanPatch() => true; //dbg
        /// <summary>
        /// Pack list of resolved conflicting files into a new bundle.
        /// </summary>
        public void Patch()
        {
            /*
            //dbg
            //load bundle
            var bundle = new Bundle(@"F:\Mods\modcleanboat\content\blob0.bundle");
            //var bundle = new Bundle(@"F:\Mods\modcleanboat\content\buffers0.bundle");
            List<IWitcherFile> patchFiles = new List<IWitcherFile>();
            foreach (var file in bundle.Items)
                patchFiles.Add(file.Value);
              
            //create metadata
            string indir = @"F:\Mods\mod0000_PatchedFiles\content";
            var ms_file = new Metadata_Store();
            ms_file.Read(Path.Combine(indir, "wmetadata.store"));

            var ms_dir = new Metadata_Store(indir);
            */

            
            List<IWitcherFileWrapper> patchFiles = ConflictsList.Select(x => x.ResolvedFile()).Where(_ => _ != null).ToList();
            List<IWitcherFile> blobFiles = patchFiles.Select(_ => _.File).ToList();
            List<IWitcherFile> bufferFiles = patchFiles.Select(_ => _.Buffer).Where(_ => _ != null).ToList();

            if (!(blobFiles.Count > 0) && !(bufferFiles.Count > 0))
                return;

                //create bundle
                string bundleDir = Path.Combine(ModDir, modPatchName, "content");
            if (!Directory.Exists(bundleDir))
                Directory.CreateDirectory(bundleDir);
            //blob
            List<Bundle> bundles = new List<Bundle>();
            if (blobFiles.Count > 0)
                bundles.Add( new Bundle(blobFiles.ToArray()));
            //buffers
            if (bufferFiles.Count > 0)
                bundles.Add(new Bundle(bufferFiles.ToArray()));
            foreach (var b in bundles)
                b.Write(bundleDir);
                

            //create metadata
            //var ms = new Metadata_Store(bundles.ToArray()); 
            //FIXME broken for custom bundle names because of reparenting bundleItems 
            //and new bundles in memory don't have filepaths
            //which are needed to compress files since trade used a memorymapped stream
            var ms = new Metadata_Store(bundleDir);
            ms.Write(bundleDir);

            ParentViewModel.ShowFinished();
            
        }

        /// <summary>
        /// Returns true if displaying Settings is allowed.
        /// </summary>
        /// <returns></returns>
        public bool CanShowSettings() => true;
        /// <summary>
        /// Switches the view to SettingsView.
        /// </summary>
        public void ShowSettings() => ParentViewModel.ShowSettings();

        #endregion
        #endregion

        #region Methods
        /// <summary>
        /// Load all bundles in the games Mods folder and selects the conflicting files.
        /// </summary>
        private void GetConflictsList()
        {
            //read out all bundles
            List<string> allFiles = new List<string>();
            List<string> allBundles = new List<string>();

            
            allBundles = Directory.GetFiles(ModDir, "*.bundle", SearchOption.AllDirectories).ToList();
            BundleManager bm = new BundleManager();
            foreach (var bundle in allBundles)
            {
                //exclude modPatchName directory
                if (!bundle.Contains(modPatchName))
                    bm.LoadBundle(bundle);
            }
                

            List<KeyValuePair<string, List<IWitcherFile>>> allConflicts = bm.Items.Where(kvp => kvp.Value.Count > 1).ToList();
            

            //cast to tw3conflict
            foreach (var c in allConflicts)
            {
                string ext = c.Key.Split('\\').Last().Split('.').Last();
                if (hiddenExts.Contains(ext))
                    continue;

                //FIXME
                // list of conflicting buffers
                if (ext == "buffer")
                {
                    List<IWitcherFile> buffers = c.Value;
                    string parentfilename = c.Key.Substring(0,c.Key.Length - 9); //FIXME trim ".1.buffer"
                    parentfilename = parentfilename.Split('\\').Last();
                    //list of files for buffers
                    var parentfiles = ConflictsList.First(_ => _.Name == parentfilename).Items.ToList();
                    //find matching parent 
                    foreach (var f in buffers)
                    {
                        var splits = f.Bundle.FileName.Split('\\').ToList();
                        var modname = splits.Where(_ => _.Length >= 3).First(_ => _.Substring(0, 3) == "mod");

                        parentfiles.First(_ => _.ToString() == modname).Buffer = f;
                    }
                    continue;
                }

                List<IWitcherFileWrapper> filesAsViewModel = new List<IWitcherFileWrapper>();
                foreach (var wf in c.Value)
                    filesAsViewModel.Add(new IWitcherFileWrapper { File=wf});

                IConflictWrapper tw3Conflict = new IConflictWrapper
                {
                    Name = c.Key.Split('\\').Last(),
                    Category = ext, //extension
                    Items = filesAsViewModel
                };
                ConflictsList.Add(tw3Conflict);
            }
        }


        #endregion
    }
}
