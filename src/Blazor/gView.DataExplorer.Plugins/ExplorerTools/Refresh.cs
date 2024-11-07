﻿using gView.DataExplorer.Plugins.Extensions;
using gView.Framework.Blazor.Services.Abstraction;
using gView.Framework.Core.Common;
using gView.Framework.DataExplorer;
using gView.Framework.DataExplorer.Abstraction;
using gView.Framework.DataExplorer.Services.Abstraction;
using System.Threading.Tasks;

namespace gView.DataExplorer.Plugins.ExplorerTools;

[RegisterPlugIn("54597B19-B37A-4fa2-BA45-8B4137F2910E")]
public class Refresh : IExplorerTool
{
    #region IExplorerTool

    public string Name => "Refresh";

    public bool IsEnabled(IExplorerApplicationScopeService scope) => true;

    public string ToolTip => "";

    public string Icon => "basic:refresh";

    public ExplorerToolTarget Target => ExplorerToolTarget.General;

    async public Task<bool> OnEvent(IExplorerApplicationScopeService scope)
    {
        await scope.ForceContentRefresh();

        return true;
    }

    #endregion

    #region IOrder

    public int SortOrder => 15;

    #endregion

    #region IDisposable

    public void Dispose()
    {

    }

    #endregion
}
