﻿using gView.Blazor.Core.Exceptions;
using gView.DataExplorer.Plugins.ExplorerObjects.Base;
using gView.DataExplorer.Plugins.ExplorerObjects.Extensions;
using gView.DataSources.OGR;
using gView.Framework.Core.Common;
using gView.Framework.Core.Data;
using gView.Framework.Core.Geometry;
using gView.Framework.DataExplorer.Abstraction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace gView.DataExplorer.Plugins.ExplorerObjects.OSGeo.Ogr;

//[RegisterPlugIn("6482DCB2-918B-417A-BD24-54C80E2202AF")]
public class OGRFeatureClassExplorerObject : ExplorerObjectCls<IExplorerObject, IFeatureClass>,
                                             IExplorerFileObject,
                                             IExplorerObjectCustomContentValues,
                                             ISerializableExplorerObject
{
    private string _filename = "", _type = "", _icon = "";
    private IFeatureClass? _fc;

    public OGRFeatureClassExplorerObject() : base() { }

    private OGRFeatureClassExplorerObject(IExplorerObject parent, string filename)
        : base(parent, 1)
    {
        _filename = filename;
    }
    async static public Task<OGRFeatureClassExplorerObject> Create(IExplorerObject parent, string filename)
    {
        var exObject = new OGRFeatureClassExplorerObject(parent, filename);

        Dataset ds = new Dataset();
        await ds.SetConnectionString(filename);
        if (!await ds.Open())
        {
            if (ds.LastErrorMessage != String.Empty)
            {
                throw new GeneralException("ERROR:" + ds.LastErrorMessage);
            }

            return exObject;
        }

        var elements = await ds.Elements();

        if (elements.Count == 1)
        {
            exObject._fc = elements[0].Class as IFeatureClass;
        }

        if (exObject._fc == null)
        {
            return exObject;
        }

        switch (exObject._fc.GeometryType)
        {
            case GeometryType.Envelope:
            case GeometryType.Polygon:
                exObject._icon = "webgis:shape-polygon";
                exObject._type = "OGR Polygon Featureclass";
                break;
            case GeometryType.Multipoint:
            case GeometryType.Point:
                exObject._icon = "basic:dot-filled";
                exObject._type = "OGR Point Featureclass";
                break;
            case GeometryType.Polyline:
                exObject._icon = "webgis:shape-polyline";
                exObject._type = "OGR Polyline Featureclass";
                break;
            default:
                exObject._icon = "basic:code-markup-box";
                exObject._type = "OGR Featureclass";
                break;
        }

        return exObject;
    }

    #region IExplorerFileObject Member

    public string Filter
    {
        get { return "*.e00|*.gml|*.00d|*.adf"; }
    }

    async public Task<IExplorerFileObject?> CreateInstance(IExplorerObject parent, string filename)
    {
        try
        {
            if (!(new FileInfo(filename)).Exists)
            {
                return null;
            }
            
            return await OGRFeatureClassExplorerObject.Create(parent, filename);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region IExplorerObjectCustomContentValues

    public IDictionary<string, object?> GetCustomContentValues()
        => _filename.GetFileProperties();

    #endregion

    #region IExplorerObject Member

    public string Name
    {
        get
        {
            try
            {
                FileInfo fi = new FileInfo(_filename);
                return fi.Name;
            }
            catch { return "???"; }
        }
    }

    public string FullName => _filename;

    public string Type => _type;

    public string Icon => _icon;

    public Task<object?> GetInstanceAsync() => Task.FromResult<object?>(_fc);

    #endregion

    #region IDisposable Member

    public void Dispose()
    {

    }

    #endregion

    #region ISerializableExplorerObject Member

    public Task<IExplorerObject?> CreateInstanceByFullName(string FullName, ISerializableExplorerObjectCache? cache)
    {
        return Task.FromResult<IExplorerObject?>(null);
    }

    #endregion
}
