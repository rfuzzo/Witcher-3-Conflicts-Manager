using System;
using System.Drawing;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WolvenKit.Bundles;
using WolvenKit.Common;
using WolvenKit.CR2W;
using WolvenKit.Cache;

namespace Witcher_3_Conflicts_Manager.ViewModels
{
    using Commands;
    using Models;
    using System.Drawing.Imaging;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;


    /// <summary>
    /// 
    /// </summary>
    public class ConflictsViewModel : ViewModel
    {
        private const string modPatchName = "mod0000_PatchedFiles";
        private readonly string[] hiddenExts = new string[] { "xml", "csv", "xbm" };

        public ConflictsViewModel()
        {
            AllMods = new List<IModWrapper>();
            BundleConflictFilesList = new Dictionary<string, List<IWitcherFile>>();
            CacheConflictFilesList = new Dictionary<string, List<IWitcherFile>>();

            //Bindings
            ConflictingMods = new ObservableCollection<IModWrapper>();
            ConflictsList = new ObservableCollection<IConflictWrapper>();


            #region Commands 
            PatchCommand = new RelayCommand(Patch, CanPatch);
            ShowSettingsCommand = new RelayCommand(ShowSettings);
            RefreshCommand = new RelayCommand(Refresh);

            IsSelectedCommand = new DelegateCommand<IWitcherFileWrapper>(Select);
            #endregion


        }

        #region Properties
        public MainViewModel ParentViewModel { get; set; }

        public ObservableCollection<IConflictWrapper> ConflictsList { get; set; }
        public Dictionary<string, List<IWitcherFile>> BundleConflictFilesList { get; set; }
        public Dictionary<string, List<IWitcherFile>> CacheConflictFilesList { get; set; }
        public ObservableCollection<IModWrapper> ConflictingMods { get; set; }
        public List<IModWrapper> AllMods { get; set; }


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

        private ImageSource _selectedImage;
        public ImageSource SelectedImage
        {
            get => _selectedImage;
            set
            {
                if (_selectedImage != value)
                {
                    _selectedImage = value;
                    OnPropertyChanged();
                }
            }
        }

        private IWitcherFileWrapper _selectedFile;
        public IWitcherFileWrapper SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (_selectedFile != value)
                {
                    _selectedFile = value;
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
        public ICommand RefreshCommand { get; }

        public ICommand IsSelectedCommand { get; }

        #region Command Implementation
        public void Select(IWitcherFileWrapper wfw)
        {
            // extract image and disply it in the imagecontrol (binding)
            if (wfw.File is TextureCacheItem)
            {
                IWitcherFile wf = wfw.File;
                using (var ms = new MemoryStream())
                {
                    wf.Extract(ms);
                    Bitmap bmp = new DdsImage(ms.ToArray()).BitmapImage;
                    SelectedImage = BitmapToImageSource(bmp);
                    bmp.Dispose();
                }
            }

            SelectedFile = wfw;
        }

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

            //BUNDLES
            List<IWitcherFileWrapper> patchFiles = ConflictsList.Select(x => x.ResolvedFile()).Where(_ => _ != null).ToList();

            List<BundleItem> blobFiles = patchFiles.Select(_ => _.File).Select(_ => _ as BundleItem).Where(_ => _ != null).ToList();
            List<BundleItem> bufferFiles = patchFiles.Select(_ => _.Buffer).Where(_ => _ != null).ToList();
            List<TextureCacheItem> cacheFiles = patchFiles.Select(_ => _.File).Select(_ => _ as TextureCacheItem).Where(_ => _ != null).ToList();

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
            //if (cacheFiles.Count > 0)
            var tc = new TextureCache(cacheFiles.ToArray());

            foreach (var b in bundles)
                b.Write(bundleDir);
            tc.Write(bundleDir);
             

            //create metadata
            var ms = new Metadata_Store(bundleDir);
            ms.Write(bundleDir);

            ParentViewModel.ShowFinished();
            
        }


        /// <summary>
        /// Switches the view to SettingsView.
        /// </summary>
        public void ShowSettings() => ParentViewModel.ShowSettings();

        #endregion
        #endregion

        #region Methods
        #region Public Methods
        /// <summary>
        /// reload all from mod directory
        /// </summary>
        public void Refresh()
        {
            //<baseGame>\bin\x64\witcher3.exe
            BaseGameDir = Path.GetFullPath(Path.Combine(Properties.Settings.Default.TW3_Path, @"..\..\..\"));
            ModDir = Path.Combine(BaseGameDir, @"Mods");

            LoadMods();

            LoadBundles();
            LoadCaches();

            ReloadAll();
        }

        /// <summary>
        /// Recreate Conflicts List for selected mods
        /// </summary>
        public void ReloadAll()
        {
            ConflictsList.Clear();
            ReloadBundleConflicts();
            ReloadCacheConflicts();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Load all mods in the Mod Directory.
        /// </summary>
        private void LoadMods()
        {
            AllMods = new List<IModWrapper>();

            //load all mods with bundles
            DirectoryInfo di = new DirectoryInfo(ModDir);
            List<DirectoryInfo> mods = di.GetDirectories("mod*", SearchOption.TopDirectoryOnly).Where(_ => _.Name != modPatchName).ToList();
            foreach (var mdi in mods)
            {
                try
                {
                    var mw = new IModWrapper(mdi);

                    var bundles = mdi.GetFiles("*.bundle", SearchOption.AllDirectories).ToList();
                    mw.Bundles = bundles.Select(_ => new Bundle(_.FullName)).ToList();

                    var caches = mdi.GetFiles("texture.cache", SearchOption.AllDirectories).ToList();
                    mw.Caches = caches.Select(_ => new TextureCache(_.FullName)).ToList();

                    AllMods.Add(mw);
                }
                catch (Exception)
                {
                    //failed to load mod, skipping
                    throw;
                }
            }
        }

        /// <summary>
        /// Load all chaches in the Mod Directory and saves the Conflicting files as dictionary. 
        /// </summary>
        private void LoadCaches()
        {
            //read conflicts for all mods
            var AllCaches = AllMods.SelectMany(_ => _.Caches).ToList();
            var Items = new Dictionary<string, List<IWitcherFile>>();
            foreach (var c in AllCaches)
            {
                foreach (var item in c.Items)
                {

                    if (!Items.ContainsKey(item.Name))
                        Items.Add(item.Name, new List<IWitcherFile>());

                    Items[item.Name].Add(item);
                }
            }

            //get conflicting files
            CacheConflictFilesList = Items.Where(kvp => kvp.Value.Count > 1).ToDictionary(_ => _.Key, _ => _.Value);

            //get conflicting mod names
            var files = CacheConflictFilesList.SelectMany(_ => _.Value);
            foreach (var f in files)
            {
                var m = AllMods.Find(_ => _.ToString() == GetModname(f));
                if (!ConflictingMods.Contains(m))
                    ConflictingMods.Add(m);
            }
        }

        /// <summary>
        /// Load all bundles in the Mod Directory and saves the Conflicting files as dictionary. 
        /// </summary>
        private void LoadBundles()
        {
            

            //read conflicts for all mods
            var AllBundles = AllMods.SelectMany(_ => _.Bundles).ToList();
            var Items = new Dictionary<string, List<IWitcherFile>>();
            foreach (var b in AllBundles)
            {
                foreach (var item in b.Items)
                {
                    //filter conflicts
                    string ext = item.Name.Split('\\').Last().Split('.').Last();
                    if (hiddenExts.Contains(ext))
                        continue;

                    if (!Items.ContainsKey(item.Name))
                        Items.Add(item.Name, new List<IWitcherFile>());

                    Items[item.Name].Add(item);
                }
            }
            
            //get conflicting files
            BundleConflictFilesList = Items.Where(kvp => kvp.Value.Count > 1).ToDictionary(_ => _.Key, _ => _.Value);

            //get conflicting mod names
            var files = BundleConflictFilesList.SelectMany(_ => _.Value);
            foreach (var f in files)
            {
                var a = AllMods.First().ToString();
                var b = GetModname(f);
                var m = AllMods.Find(_ => _.ToString() == GetModname(f));
                if (!ConflictingMods.Contains(m))
                    ConflictingMods.Add(m);
            }
                
        }

        /// <summary>
        /// Populate Conflicts List of selected mods.
        /// </summary>
        private void ReloadCacheConflicts()
        {
            
            var selectedMods = ConflictingMods.Where(_ => _.IsSelected == true).ToList();

            //loop through all conflicting files and cast to tw3conflict for selected mods
            foreach (var c in CacheConflictFilesList)
            {
                try
                {
                    // continue for unselected mods
                    var filesAsViewModel = new ObservableCollection<IWitcherFileWrapper>();
                    foreach (var wf in c.Value)
                    {
                        // if the file's mod can be found in the modlist add to conflicts
                        if (selectedMods.Any(_ => _.ToString() == GetModname(wf)))
                        {
                            filesAsViewModel.Add(new IWitcherFileWrapper { File = wf });
                        }
                    }
                    if (filesAsViewModel.Count < 2)
                        continue;

                    

                    // add to list
                    IConflictWrapper tw3Conflict = new IConflictWrapper
                    {
                        Name = c.Key.Split('\\').Last(),
                        Category = c.Key.Split('\\').First(),
                        Items = filesAsViewModel,
                    };
                    ConflictsList.Add(tw3Conflict);
                }
                catch (Exception)
                {
                    //failed to add conflict, skipping
                    continue;
                }
            }
        }
       
        /// <summary>
        /// Populate Conflicts List of selected mods.
        /// </summary>
        private void ReloadBundleConflicts()
        {
            
            var selectedMods = ConflictingMods.Where(_ => _.IsSelected == true).ToList();

            //loop through all conflicting files and cast to tw3conflict for selected mods
            foreach (var c in BundleConflictFilesList)
            {
                try
                {
                    // continue for unselected mods
                    var filesAsViewModel = new ObservableCollection<IWitcherFileWrapper>();
                    foreach (var wf in c.Value)
                    {
                        // if the file's mod can be found in the modlist add to conflicts
                        if (selectedMods.Any(_ => _.ToString() == GetModname(wf)))
                        {
                            filesAsViewModel.Add(new IWitcherFileWrapper { File = wf });
                        }
                    }
                    if (filesAsViewModel.Count < 2)
                        continue;
                    

                    // single out and add buffers to files
                    string ext = c.Key.Split('\\').Last().Split('.').Last();
                    if (ext == "buffer")
                    {
                        List<BundleItem> buffers = c.Value.Select(_ => _ as BundleItem).ToList();
                        string parentfilename = c.Key.Substring(0, c.Key.Length - 9); //trim ".1.buffer"
                        parentfilename = parentfilename.Split('\\').Last();
                        //list of files for buffers
                        var parentfiles = ConflictsList.First(_ => _.Name == parentfilename).Items.ToList();
                        //find matching parent 
                        foreach (var f in buffers)
                        {
                            //var splits = f.Bundle.FileName.Split('\\').ToList();
                            //var modname = splits.Where(_ => _.Length >= 3).First(_ => _.Substring(0, 3) == "mod");
                            var modname = GetModname(f);

                            parentfiles.First(_ => _.ToString() == modname).Buffer = f;
                        }
                        continue;
                    }

                    // add to list
                    IConflictWrapper tw3Conflict = new IConflictWrapper
                    {
                        Name = c.Key.Split('\\').Last(),
                        Category = ext, //extension
                        Items = filesAsViewModel
                    };
                    ConflictsList.Add(tw3Conflict);
                }
                catch (Exception)
                {
                    //failed to add conflict, skipping
                    continue;
                }
            }
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }


        private static string GetModname(IWitcherFile f)
        {
            var BaseGameDir = Path.GetFullPath(Path.Combine(Properties.Settings.Default.TW3_Path, @"..\..\..\"));
            var ModDir = Path.Combine(BaseGameDir, @"Mods");
            var s = GetRelativePath(f.Bundle.FileName, ModDir);
            string modname = s.Split('\\').Where(_ => _.Length >= 3).First(x => x.Substring(0, 3) == "mod");
            

            return modname;
        }

        /// <summary>
        /// Gets relative path from absolute path.
        /// </summary>
        /// <param name="filespec">A files path.</param>
        /// <param name="folder">The folder's path.</param>
        /// <returns></returns>
        private static string GetRelativePath(string filespec, string folder)
        {
            Uri pathUri = new Uri(filespec);
            // Folders must end in a slash
            if (!folder.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                folder += Path.DirectorySeparatorChar;
            }
            Uri folderUri = new Uri(folder);
            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
        #endregion
        #endregion
    }
}
