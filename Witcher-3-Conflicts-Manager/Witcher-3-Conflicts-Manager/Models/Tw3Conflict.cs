using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WolvenKit.Common;

namespace Witcher_3_Conflicts_Manager.Models
{
    public class IWitcherFileViewModel
    {
        public bool? IsChecked { get; set; }
        public IWitcherFile File { get; set; }
    }



    public class BundleConflict: ObservableObject
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

        private List<IWitcherFile> _items;
        public List<IWitcherFile> Items
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

        private bool _resolved;
        public bool Resolved
        {
            get => _resolved;
            set
            {
                if (_resolved != value)
                {
                    _resolved = value;
                    OnPropertyChanged();
                }
            }
        }

        private IWitcherFile _resolvedFile;
        public IWitcherFile ResolvedFile
        {
            get => _resolvedFile;
            set
            {
                if (_resolvedFile != value)
                {
                    _resolvedFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
