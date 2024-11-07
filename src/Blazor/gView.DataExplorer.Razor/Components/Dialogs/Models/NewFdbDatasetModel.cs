﻿using gView.Blazor.Models.Dialogs;
using gView.Framework.Core.FDB;
using gView.Framework.Core.Geometry;
using gView.Framework.Data;
using gView.Framework.Common;

namespace gView.DataExplorer.Razor.Components.Dialogs.Models;

public class NewFdbDatasetModel : IDialogResultItem
{
    public NewFdbDatasetModel()
        : this(new BinaryTreeDef(new Framework.Geometry.Envelope(), 5))
    {
    }
    public NewFdbDatasetModel(BinaryTreeDef spatialIndex)
    {
        this.SpatialIndex = spatialIndex;

        PlugInManager pluginManager = new PlugInManager();
        foreach (Type t in pluginManager.GetPlugins(gView.Framework.Common.Plugins.Type.IAutoField))
        {
            var autoField = (IAutoField)pluginManager.CreateInstance(t);
            if(autoField is Field)
            {
                ((Field)autoField).name = autoField.AutoFieldPrimayName;
            }
            AutoFields.Add(autoField, false);
        }
    }

    public string Name { get; set; } = string.Empty;

    public NewFdbDatasetType DatasetType { get; set; }

    public ISpatialReference? SpatialReference { get; set; }

    public BinaryTreeDef SpatialIndex { get; set; }

    public Dictionary<IAutoField, bool> AutoFields { get; set; } = new Dictionary<IAutoField, bool>();
}
