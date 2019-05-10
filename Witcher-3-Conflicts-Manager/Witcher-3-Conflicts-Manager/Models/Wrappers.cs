using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Witcher_3_Conflicts_Manager.Commands;
using WolvenKit.Bundles;
using WolvenKit.Cache;
using WolvenKit.Common;

namespace Witcher_3_Conflicts_Manager.Models
{

    /// <summary>
    /// Wrapper Class for Bundles
    /// </summary>
    public class IBundleWrapper : ObservableObject
    {
        public IBundleWrapper(IWitcherArchive bundle)
        {
            Bundle = bundle;
        }

        private string _modName;
        public string ModName
        {
            get => _modName;
            set
            {
                if (_modName != value)
                {
                    _modName = value;
                    OnPropertyChanged();
                }
            }
        }

        public IWitcherArchive Bundle { get; set; }

        public override string ToString()
        {
            return Bundle.ToString();
        }
    }

    /// <summary>
    /// Wrapper Class for Mods
    /// </summary>
    public class IModWrapper : ObservableObject
    {
        public IModWrapper(DirectoryInfo dir)
        {
            IsSelected = true;
            Dir = dir;
        }

        private bool? _isSelected;
        public bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public DirectoryInfo Dir { get; set; }
        public List<Bundle> Bundles { get; set; }
        public List<TextureCache> Caches { get; set; }

        public override string ToString()
        {
            return Dir.Name;
        }
    }



    /// <summary>
    /// Wrapper Class for one conflicting file for displaying in a Radio-button List.
    /// </summary>
    public class IWitcherFileWrapper : ObservableObject
    {
        public IWitcherFileWrapper()
        {
            IsChecked = false;
        }

        #region Properties
        public FontWeight FontWeight
        {
            get
            {
                if (IsChecked == true)
                {
                    return FontWeights.Bold;
                }
                else
                {
                    return FontWeights.Normal;
                }
            }
        }
        private bool? _isChecked;
        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                    OnPropertyChanged("FontWeight");
                }
            }
        }
        public IWitcherFile File { get; set; }
        public BundleItem Buffer { get; set; }
        public ImageSource Image { get; set; }
        #endregion

        public override string ToString()
        {
            return File.Bundle.FileName.Split('\\').Where(_ => _.Length >= 3).Last(_ => _.Substring(0, 3) == "mod");
        }


    }


    /// <summary>
    /// Wrapper Class for a list of conflicting file for displaying in a List.
    /// </summary>
    public class IConflictWrapper: ObservableObject
    {

        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _category;
        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<IWitcherFileWrapper> _items;
        public ObservableCollection<IWitcherFileWrapper> Items
        {
            get => _items;
            set
            {
                if (_items != value)
                {
                    _items = value;
                    OnPropertyChanged();
                }
            }
        }


        #region Methods
        /// <summary>
        /// Returns true if one conflicting file has been selected.
        /// </summary>
        /// <returns></returns>
        public bool Resolved() => ResolvedFile() != null;
        /// <summary>
        /// Returns the selected File from a list of conflicting files. 
        /// </summary>
        /// <returns></returns>
        public IWitcherFileWrapper ResolvedFile() => Items.FirstOrDefault(x => x.IsChecked ?? false);
        #endregion



        public override string ToString()
        {
            return Name;
        }
    }
}
