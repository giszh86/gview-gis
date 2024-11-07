﻿using gView.Cmd.Core.Abstraction;
using gView.Cmd.Fdb.Lib;
using gView.DataExplorer.Plugins.Extensions;
using gView.DataExplorer.Razor.Components.Dialogs.Models;
using gView.DataSources.Fdb.MSAccess;
using gView.Framework.Blazor;
using gView.Framework.Blazor.Services.Abstraction;
using gView.Framework.Core.Data;
using gView.Framework.Data;
using gView.Framework.DataExplorer.Abstraction;
using gView.Framework.Common;
using gView.Razor.Base;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using gView.Framework.DataExplorer.Services.Abstraction;

namespace gView.DataExplorer.Plugins.ExplorerObjects.Fdb.ContextTools;
internal class SpatialIndexDefinition : IExplorerObjectContextTool
{
    public string Name => "Spatial Index Definition...";

    public string Icon => "basic:warning_yellow";

    public bool IsEnabled(IExplorerApplicationScopeService scope, IExplorerObject exObject)
    {
        return true;
    }

    async public Task<bool> OnEvent(IExplorerApplicationScopeService scope, IExplorerObject exObject)
    {
        var featureClass = await exObject.GetInstanceAsync() as IFeatureClass;
        var fdb = featureClass?.Dataset?.Database as AccessFDB;

        if (featureClass == null || fdb == null)
        {
            throw new Exception("Instance is not an valid FDB Featureclass!");
        }

        var binarayTreeDef = await fdb.BinaryTreeDef(featureClass.Name);

        var model = await scope.ShowModalDialog(typeof(gView.DataExplorer.Razor.Components.Dialogs.SpatialIndexDefDialog),
                                           "Spatial Index Definition",
                                           new BaseDialogModel<BinaryTreeDef>() { Value = binarayTreeDef });

        if (model?.Value?.Bounds == null)
        {
            return false;
        }


        IDictionary<string, object>? parameters = null;
        ICommand? command = null;

        var featureDataset = featureClass.Dataset;
        var featureDatasetGuid = PlugInManager.PlugInID(featureDataset);

        command = new RebuildSpatiallIndexDefCommand();
        parameters = new Dictionary<string, object>()
            {
                { "dataset_connstr", featureDataset.ConnectionString },
                { "dataset_guid", featureDatasetGuid.ToString() },
                { "dataset_fc", featureClass.Name },
                { "bounds_minx", model.Value.Bounds.MinX },
                { "bounds_miny", model.Value.Bounds.MinY },
                { "bounds_maxx", model.Value.Bounds.MaxX },
                { "bounds_maxy", model.Value.Bounds.MaxY },
                { "max_levels", model.Value.MaxLevel }
            };

        await scope.ShowKnownDialog(
                    KnownDialogs.ExecuteCommand,
                    $"Rebuild spatial index",
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
                    });

        return true;
    }
}
