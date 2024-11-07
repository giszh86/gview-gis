﻿using gView.Cmd.Core.Abstraction;
using gView.Cmd.Fdb.Lib;
using gView.Cmd.Fdb.Lib.Model;
using gView.DataExplorer.Razor.Components.Dialogs.Models;
using gView.DataSources.Fdb.MSSql;
using gView.DataSources.Fdb.PostgreSql;
using gView.DataSources.Fdb.SQLite;
using gView.Framework.Blazor;
using gView.Framework.Core.Data;
using gView.Framework.DataExplorer.Abstraction;
using gView.Framework.DataExplorer.Services.Abstraction;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace gView.DataExplorer.Plugins.ExplorerObjects.Fdb.ContextTools;
internal class AddImages : IExplorerObjectContextTool
{
    public string Name => "Add Image Folder";

    public string Icon => "basic:folder-white";

    public bool IsEnabled(IExplorerApplicationScopeService scope, IExplorerObject exObject)
    {
        return true;
    }

    async public Task<bool> OnEvent(IExplorerApplicationScopeService scope, IExplorerObject exObject)
    {
        var instance = await exObject.GetInstanceAsync() as IRasterCatalogClass;

        var model = await scope.ShowModalDialog(typeof(gView.DataExplorer.Razor.Components.Dialogs.ImageDatasetImportFolder),
                                                       "Import Folder",
                                                        new ImageDatasetImportFolderModel());

        if (!string.IsNullOrEmpty(model?.Folder))
        {
            IDictionary<string, object> parameters = new Dictionary<string, object>();
            ICommand command = new ImageDatasetUtilCommand();

            parameters.Add("fdb", instance switch
            {
                SqlFDBImageCatalogClass => "sql",
                pgImageCatalogClass => "postgres",
                SQLiteFDBImageCatalogClass => "sqlite",
                _ => throw new System.Exception($"Can't add images to {instance?.GetType().Name}")
            });
            parameters.Add("connstr", instance.Dataset.ConnectionString);

            parameters.Add("job", "add");
            parameters.Add("directory", model.Folder);
            if (!string.IsNullOrEmpty(model.Filter))
            {
                parameters.Add("filter", model.Filter);
            }

            if (model.Providers != null && model.Providers.Any(p => p.Selected))
            {
                if (string.IsNullOrEmpty(model.Filter))
                {
                    parameters.Add("filter", string.Join(";", model.Providers
                        .Where(p => p.Selected)
                        .Select(p => p.Format)));
                }

                parameters.Add("provider", JsonSerializer.Serialize(
                        model.Providers
                            .Where(p => p.Selected)
                            .OrderByDescending(p => p.Priority)
                            .Select(p => new RasterProviderModel()
                            {
                                PluginGuid = p.PluginGuid,
                                Format = p.Format
                            })
                    ));
            }

            if (await scope.ShowKnownDialog(
                    KnownDialogs.ExecuteCommand,
                    $"Add File",
                    new ExecuteCommandModel()
                    {
                        CommandItems = new[]
                        {
                            new CommandItem()
                            {
                                Command = command,
                                Parameters = parameters
                            }
                        }
                    }) != null)
            {
                await scope.ForceContentRefresh();
            }

            return true;
        }

        return false;
    }
}
