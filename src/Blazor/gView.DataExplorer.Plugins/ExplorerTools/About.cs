﻿using gView.DataExplorer.Plugins.Extensions;
using gView.Framework.Blazor.Services.Abstraction;
using gView.Framework.Core.Common;
using gView.Framework.DataExplorer;
using gView.Framework.DataExplorer.Abstraction;
using gView.Framework.Common;
using gView.Razor.Dialogs.Models;
using System.Threading.Tasks;
using gView.DataExplorer.Core.Services.Abstraction;
using gView.Framework.DataExplorer.Services.Abstraction;

namespace gView.DataExplorer.Plugins.ExplorerTools;

[RegisterPlugIn("1DB93AF4-F1E0-4CD7-A934-0E8BE3C35D98")]
public class About : IExplorerTool
{
    #region IExplorerTool

    public string Name => "About";

    public string ToolTip => "";

    public string Icon => "basic:help";

    public ExplorerToolTarget Target => ExplorerToolTarget.About;

    public bool IsEnabled(IExplorerApplicationScopeService scope) => true;

    async public Task<bool> OnEvent(IExplorerApplicationScopeService scope)
    {
        await scope.ShowModalDialog(typeof(gView.Razor.Dialogs.AboutDialog),
                                     "About",
                                     new AboutDialogModel()
                                     {
                                         Title = "gView GIS DataExplorer",
                                         Version = SystemInfo.Version
                                     });

        return true;
    }

    #endregion

    #region IOrder

    public int SortOrder => 999;

    #endregion

    #region IDisposable

    public void Dispose()
    {

    }

    #endregion
}
