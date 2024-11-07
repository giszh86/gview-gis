﻿using gView.DataExplorer.Plugins.ExplorerObjects.Base;
using gView.DataExplorer.Plugins.Extensions;
using gView.DataExplorer.Razor.Components.Dialogs.Models;
using gView.DataExplorer.Razor.Components.Dialogs.Models.Extensions;
using gView.Framework.Blazor.Services.Abstraction;
using gView.Framework.Core.Common;
using gView.Framework.DataExplorer.Abstraction;
using gView.Framework.DataExplorer.Events;
using gView.Framework.IO;
using System.Threading.Tasks;
using gView.Framework.Common.Extensions;
using gView.Framework.DataExplorer.Services.Abstraction;
using gView.DataExplorer.Core.Extensions;

namespace gView.DataExplorer.Plugins.ExplorerObjects.Web.ArcIms;

[RegisterPlugIn("1B6F2AF5-9146-498C-9C71-8C69E153FD35")]
public class ArcImsNewConnectionExplorerObject : ExplorerObjectCls<IExplorerObject>,
                                                 IExplorerSimpleObject,
                                                 IExplorerObjectDoubleClick,
                                                 IExplorerObjectCreatable
{
    public ArcImsNewConnectionExplorerObject()
        : base()
    {
    }

    public ArcImsNewConnectionExplorerObject(IExplorerObject parent)
        : base(parent, 0)
    {
    }

    #region IExplorerObject Member

    public string Name => "New ArcIMS Connection...";

    public string FullName => "";

    public string Type => "New Connection";

    public string Icon => "basic:edit-plus";

    public void Dispose()
    {

    }

    public Task<object?> GetInstanceAsync() => Task.FromResult<object?>(null);

    #endregion

    #region IExplorerObjectDoubleClick Member

    async public Task ExplorerObjectDoubleClick(IExplorerApplicationScopeService appScope, ExplorerObjectEventArgs e)
    {
        var model = await appScope.ShowModalDialog(
            typeof(gView.DataExplorer.Razor.Components.Dialogs.ArcImsConnectionDialog),
            "GeoServices Connection",
            new ArcImsConnectionModel());

        if (model != null)
        {
            var connectionString = model.ToConnectionString();
            ConfigConnections connStream = ConfigConnections.Create(
                    this.ConfigStorage(),
                    "arcims_connections", 
                    "546B0513-D71D-4490-9E27-94CD5D72C64A"
                );

            string id = connStream.GetName(ConfigTextStream.ExtractValue(connectionString, "server").UrlToConfigId());
            connStream.Add(id, connectionString);

            e.NewExplorerObject = new ArcImsConnectionExplorerObject(Parent, id, connectionString);
        }
    }

    #endregion

    #region ISerializableExplorerObject Member

    public Task<IExplorerObject?> CreateInstanceByFullName(string FullName, ISerializableExplorerObjectCache? cache)
    {
        if (cache != null && cache.Contains(FullName))
        {
            return Task.FromResult(cache[FullName]);
        }

        return Task.FromResult<IExplorerObject?>(null);
    }

    #endregion

    #region IExplorerObjectCreatable Member

    public bool CanCreate(IExplorerObject parentExObject)
    {
        return (parentExObject is ArcImsExplorerObjects);
    }

    async public Task<IExplorerObject?> CreateExplorerObjectAsync(IExplorerApplicationScopeService appScope, IExplorerObject parentExObject)
    {
        ExplorerObjectEventArgs e = new ExplorerObjectEventArgs();
        await ExplorerObjectDoubleClick(appScope, e);
        return e.NewExplorerObject;
    }

    #endregion
}
