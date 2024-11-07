﻿using gView.DataExplorer.Plugins.ExplorerObjects.Base;
using gView.DataExplorer.Plugins.Extensions;
using gView.DataExplorer.Razor.Components.Dialogs.Models;
using gView.DataSources.TileCache;
using gView.Framework.Blazor.Services.Abstraction;
using gView.Framework.DataExplorer.Abstraction;
using gView.Framework.DataExplorer.Events;
using gView.Framework.Common;
using System.Threading.Tasks;
using gView.Framework.DataExplorer.Services.Abstraction;

namespace gView.DataExplorer.Plugins.ExplorerObjects.Tiles.Raster;

//[RegisterPlugIn("a4196c6a-0431-4f17-af9e-97b4a2abf8dd")]
public class TileCacheLocalCacheProperties : ExplorerObjectCls<TileCacheGroupExplorerObject>,
                                             IExplorerSimpleObject,
                                             IExplorerObjectDoubleClick,
                                             IExplorerObjectCreatable
{
    public TileCacheLocalCacheProperties()
        : base() { }
    public TileCacheLocalCacheProperties(TileCacheGroupExplorerObject parent)
        : base(parent, 1) { }

    #region IExplorerSimpleObject Members

    public string Icon => "basic:settings";

    #endregion

    #region IExplorerObject Members

    public string Name => "Local Caching Properties...";

    public string FullName => "";

    public string Type => "Properties";

    public void Dispose()
    {

    }

    public Task<object?> GetInstanceAsync() => Task.FromResult<object?>(null);

    #endregion

    #region IExplorerObjectDoubleClick Members

    async public Task ExplorerObjectDoubleClick(IExplorerApplicationScopeService appScope, ExplorerObjectEventArgs e)
    {
        var model = await appScope.ShowModalDialog(typeof(gView.DataExplorer.Razor.Components.Dialogs.RasterTileCacheLocalPropertiesDialog),
                                                                    "Import from Template",
                                                                    new RasterTileCacheLocalPropertiesModel()
                                                                    {
                                                                        LocalCacheFolder = LocalCachingSettings.LocalCachingFolder,
                                                                        UseLocalCache = LocalCachingSettings.UseLocalCaching
                                                                    });

        if (model != null)
        {
            LocalCachingSettings.LocalCachingFolder = model.LocalCacheFolder;
            LocalCachingSettings.UseLocalCaching = model.UseLocalCache;
            LocalCachingSettings.Commit();
        }
    }

    #endregion

    #region ISerializableExplorerObject Member

    public Task<IExplorerObject?> CreateInstanceByFullName(string FullName, ISerializableExplorerObjectCache? cache)
    {
        if (cache?.Contains(FullName) == true)
        {
            return Task.FromResult<IExplorerObject?>(cache[FullName]);
        }

        return Task.FromResult<IExplorerObject?>(null);
    }

    #endregion

    #region IExplorerObjectCreatable Member

    public bool CanCreate(IExplorerObject parentExObject)
    {
        return (parentExObject is TileCacheGroupExplorerObject);
    }

    async public Task<IExplorerObject?> CreateExplorerObjectAsync(IExplorerApplicationScopeService appScope, IExplorerObject parentExObject)
    {
        await ExplorerObjectDoubleClick(appScope, new ExplorerObjectEventArgs());
        return null;
    }

    #endregion
}
