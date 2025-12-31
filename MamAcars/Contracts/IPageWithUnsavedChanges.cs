using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MamAcars.Contracts
{
    public interface IPageWithUnsavedChanges
    {
        bool HasUnsavedChanges { get; }
    }

}
