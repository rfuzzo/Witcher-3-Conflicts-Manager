using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using WolvenKit.Common;

namespace Witcher_3_Conflicts_Manager.Models
{
    /// <summary>
    /// Wrapper Class for one conflicting file for displaying in a Radio-button List.
    /// </summary>
    public class IWitcherFileWrapper : ObservableObject
    {
        public IWitcherFileWrapper()
        {
            IsChecked = false;
        }
        
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



        public override string ToString()
        {
            var splits = File.Bundle.FileName.Split('\\').ToList();
            var modname = splits.Where(_ => _.Length >= 3).First(_ => _.Substring(0, 3) == "mod");

            return modname;
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

        private List<IWitcherFileWrapper> _items;
        public List<IWitcherFileWrapper> Items
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
        public IWitcherFile ResolvedFile() => Items.FirstOrDefault(x => x.IsChecked ?? false)?.File;
        #endregion



        public override string ToString()
        {
            return Name;
        }
    }
}
