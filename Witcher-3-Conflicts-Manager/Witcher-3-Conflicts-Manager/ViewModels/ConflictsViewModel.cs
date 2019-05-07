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
                    //FIXME dispose of images when conflict is changed
                    if (_selectedConflict != null)
                    {
                        foreach (var item in _selectedConflict.Items)
                        {
                            if (item != null)
                                item.Image = null;
                        }  
                    }
                     

                    _selectedConflict = value;
                    OnPropertyChanged();

                    


                }
            }
        }

        private IWitcherFileWrapper _selectedFile;
        public IWitcherFileWrapper SelectedFile
        {
            get {
                return _selectedFile;
            }
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
            if (wfw.Image == null && wfw.File is TextureCacheItem)
            {
                IWitcherFile wf = wfw.File;
                using (var ms = new MemoryStream())
                {
                    wf.Extract(ms);
                    Bitmap bmp = new DdsImage(ms.ToArray()).BitmapImage;
                    wfw.Image = BitmapToImageSource(bmp);
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
            #region Debug
            /*
                        //dbg ++
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

                        DirectoryInfo di = new DirectoryInfo(ModDir);
                        List<DirectoryInfo> mods = di.GetDirectories("mod*", SearchOption.TopDirectoryOnly).Where(_ => _.Name != modPatchName).ToList();
                        foreach (var mdi in mods)
                        {
                            try
                            {

                                var tc = mdi.GetFiles("texture.cache", SearchOption.AllDirectories).ToList(); 
                                var tcaches =  tc.Select(_ => new TextureCache(_.FullName)).ToList();

                                //var sc = mdi.GetFiles("soundspc.cache", SearchOption.AllDirectories).ToList();
                                //var scaches = sc.Select(_ => new TextureCache(_.FullName)).ToList();


                            }
                            catch (Exception)
                            {
                                //failed to load mod, skipping
                                throw;
                            }
                        }
            //DirectoryInfo di = new DirectoryInfo(ModDir);
            //List<DirectoryInfo> mods = di.GetDirectories("mod*", SearchOption.TopDirectoryOnly).Where(_ => _.Name != modPatchName).ToList();
            //var mdi = mods[1];

            var tc = new TextureCache(@"E:\_test\texturecaches\_in\texture.cache");
            //var tc = new TextureCache(@"E:\moddingdir_tw3\MODSarchive\modW3EEMain\content\texture.cache");
            //var tc = new TextureCache(@"E:\moddingdir_tw3\MODSarchive\modmargarittaclean\content\texture.cache");
            var ntc = new TextureCache(tc.Items.ToArray());

            tc.Write(@"E:\_test\texturecaches\reread");
            ntc.Write(@"E:\_test\texturecaches\recreated");

            //string outpath = Path.Combine(@"E:\", $"{tc.Items.First().Name.Split('\\').Last()}.dds");
            //tc.Items.First().Extract(outpath);


            //var bpath = mdi.GetFiles("blob0.bundle", SearchOption.AllDirectories).First();
            //var b = new Bundle(bpath.FullName);
            //b.Write("E:\\");
            //dbg --

            
            //CACHES
            List<IWitcherFileWrapper> patchFiles = ConflictsList.Select(x => x.ResolvedFile()).Where(_ => _ != null).ToList();
            List<IWitcherFile> cacheFiles = patchFiles.Select(_ => _.File).ToList();
            //create cache
            string bundleDir = Path.Combine(ModDir, modPatchName, "extracted");
            if (!Directory.Exists(bundleDir))
                Directory.CreateDirectory(bundleDir);
            foreach (var cf in cacheFiles)
            {
                
            } */
            
            #endregion

            
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
            //var ms = new Metadata_Store(bundles.ToArray()); 
            //FIXME broken for custom bundle names because of reparenting bundleItems 
            //and new bundles in memory don't have filepaths
            //which are needed to compress files since trade used a memorymapped stream
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

                    var caches = mdi.GetFiles("texture.cache", SearchOption.AllDirectories).ToList(); //fixme soundcaches?
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
                foreach (var item in b.ItemsList)
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
                        string parentfilename = c.Key.Substring(0, c.Key.Length - 9); //FIXME trim ".1.buffer"
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
