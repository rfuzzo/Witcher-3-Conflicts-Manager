using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Witcher_3_Conflicts_Manager.Commands
{
    public class DelegateCommand : DelegateCommand<object>
    {
        public DelegateCommand(Action<object> execute)
            : this(execute, null)
        {

        }
        public DelegateCommand(Action<object> execute, Predicate<object> canExecute)
            : base(execute, canExecute)
        {
            
        }
    }
}