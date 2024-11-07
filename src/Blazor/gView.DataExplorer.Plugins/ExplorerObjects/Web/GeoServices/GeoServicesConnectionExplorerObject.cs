﻿using gView.DataExplorer.Plugins.ExplorerObjects.Base;
using gView.DataExplorer.Plugins.ExplorerObjects.Web.GeoServices.ContextTools;
using gView.Framework.DataExplorer.Abstraction;
using gView.Framework.DataExplorer.Events;
using gView.Framework.IO;
using gView.Framework.Web.Services;
using gView.Interoperability.GeoServices.Dataset;
using gView.Interoperability.GeoServices.Rest.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using gView.Framework.Web.Extensions;
using gView.Framework.Common.Extensions;
using gView.DataExplorer.Core.Extensions;
using gView.Framework.Core.IO;

namespace gView.DataExplorer.Plugins.ExplorerObjects.Web.GeoServices;

public class GeoServicesConnectionExplorerObject : ExplorerParentObject<IExplorerObject>,
                                                   IExplorerSimpleObject,
                                                   IExplorerObjectDeletable,
                                                   IExplorerObjectRenamable,
                                                   IExplorerObjectContextTools,
                                                   IExplorerObjectAccessability
{
    private string _name = "";
    internal string _connectionString = "";
    private IEnumerable<IExplorerObjectContextTool>? _contextTools = null;

    public GeoServicesConnectionExplorerObject() : base() { }
    internal GeoServicesConnectionExplorerObject(IExplorerObject parent, string name, string connectionString)
        : base(parent, 0)
    {
        _name = name;
        _connectionString = connectionString;

        _contextTools = new IExplorerObjectContextTool[]
        {
            new UpdateConnectionString()
        };
    }

    public string GetConnectionString() => _connectionString;

    public Task<bool> UpdateConnectionString(string connectionString)
    {
        var configConnections = GetConfigConnections();
        configConnections.Add(_name, connectionString);

        _connectionString = connectionString;

        return Task.FromResult(true);
    }

    #region IExplorerObject Member

    public string Name => _name;


    public string FullName => @$"WebServices\gView.GeoServices\{_name}";

    public string Type => "gView.GeoServices Connection";

    public string Icon => "basic:globe";

    public Task<object?> GetInstanceAsync() => Task.FromResult<object?>(null);

    #endregion

    #region IExplorerObjectContextTools

    public IEnumerable<IExplorerObjectContextTool> ContextTools => _contextTools ?? Array.Empty<IExplorerObjectContextTool>();

    #endregion

    #region IExplorerParentObject Member

    async public override Task<bool> Refresh()
    {
        await base.Refresh();

        try
        {
            string server = ConfigTextStream.ExtractValue(_connectionString, "server");
            string user = ConfigTextStream.ExtractValue(_connectionString, "user");
            string pwd = ConfigTextStream.ExtractValue(_connectionString, "pwd");

            var url = server.UrlAppendParameters("f=json");

            if (!String.IsNullOrEmpty(user) && !String.IsNullOrEmpty(pwd))
            {
                string token = await RequestTokenCache.RefreshTokenAsync(server, user, pwd);
                url = url.UrlAppendParameters($"token={token}");
            }

            var jsonServices = await HttpService.CreateInstance().GetAsync<JsonServicesDTO>(url);

            if (jsonServices != null)
            {
                if (jsonServices.Folders != null)
                {
                    foreach (var folder in jsonServices.Folders)
                    {
                        base.AddChildObject(
                            new GeoServicesFolderExplorerObject(
                                this,
                                folder,
                                _connectionString)
                            );
                    }
                }
                if (jsonServices.Services != null)
                {
                    foreach (var service in jsonServices.Services.Where(s => s.Type.ToLower() == "mapserver"))
                    {
                        base.AddChildObject(
                            new GeoServicesServiceExplorerObject(
                                this,
                                service.ServiceName,
                                String.Empty,
                                _connectionString));
                    }
                }
            }

            return true;
        }
        catch // (Exception ex)
        {
            throw;
        }
    }

    #endregion

    #region ISerializableExplorerObject Member

    async public Task<IExplorerObject?> CreateInstanceByFullName(string FullName, ISerializableExplorerObjectCache? cache)
    {
        if (cache != null && cache.Contains(FullName))
        {
            return cache[FullName];
        }

        GeoServicesExplorerObjects group = new GeoServicesExplorerObjects();
        if (FullName.IndexOf(group.FullName) != 0 || FullName.Length < group.FullName.Length + 2)
        {
            return null;
        }

        var exObject = (await group.ChildObjects()).Where(e => FullName.Equals(e.FullName)).FirstOrDefault();

        if (exObject != null)
        {
            cache?.Append(exObject);
            return exObject;
        }

        return null;
    }

    #endregion

    #region IExplorerObjectDeletable Member

    public event ExplorerObjectDeletedEvent? ExplorerObjectDeleted = null;

    public Task<bool> DeleteExplorerObject(ExplorerObjectEventArgs e)
    {
        var configConnections = GetConfigConnections();
        configConnections.Remove(this.Name);

        if (ExplorerObjectDeleted != null)
        {
            ExplorerObjectDeleted(this);
        }

        return Task.FromResult(true);
    }

    #endregion

    #region IExplorerObjectRenamable Member

    public event ExplorerObjectRenamedEvent? ExplorerObjectRenamed = null;

    public Task<bool> RenameExplorerObject(string newName)
    {
        var configConnections = GetConfigConnections();
        bool result = configConnections.Rename(this.Name, newName);

        if (result == true)
        {
            _name = newName;
            if (ExplorerObjectRenamed != null)
            {
                ExplorerObjectRenamed(this);
            }
        }
        return Task.FromResult(result);
    }

    #endregion

    #region IExplorerObjectAccessability

    public ConfigAccessability Accessability
    {
        get => GetConfigConnections().GetAccessability(_name);
        set => GetConfigConnections().SetAccessability(_name, value);
    }

    #endregion

    #region Helper

    private ConfigConnections GetConfigConnections()
        => ConfigConnections.Create(
                this.ConfigStorage(),
                "geoservices_connection",
                "546B0513-D71D-4490-9E27-94CD5D72C64A"
            );

    #endregion
}
