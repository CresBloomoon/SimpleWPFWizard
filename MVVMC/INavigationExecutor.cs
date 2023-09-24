using MVVMC.Enums;
using System.Collections.Generic;

namespace MVVMC
{
    public interface INavigationExecutor
    {
        MVVMCViewModel ExecuteNavigation(string controllerID, string pageName, object parameter, MVVMCViewModel viewModel, NavigationMode navigationMode, Dictionary<string, object> viewBag = null);
    }
}