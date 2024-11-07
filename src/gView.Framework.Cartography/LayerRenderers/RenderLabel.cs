﻿using gView.Framework.Core.Carto;
using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Cursors;
using gView.Framework.Core.Data.Filters;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.Common;
using gView.Framework.Data.Filters;
using gView.Framework.Common;
using gView.GraphicsEngine.Extensions;
using System;
using System.Threading.Tasks;
using gView.Framework.Cartography.Extensions;

namespace gView.Framework.Cartography.LayerRenderers
{
    public sealed class RenderLabel
    {
        private Map _map;
        private IFeatureLayer _layer;
        private IDatasetCachingContext _datasetCachingContext;
        private ICancelTracker _cancelTracker;
        private FeatureCounter _counter;

        public RenderLabel(Map map,
                           IDatasetCachingContext datasetCachingContext,
                           IFeatureLayer layer, 
                           ICancelTracker cancelTracker, 
                           FeatureCounter counter)
        {
            _map = map;
            _datasetCachingContext = datasetCachingContext;
            _layer = layer;
            _cancelTracker = cancelTracker == null ? new CancelTracker() : cancelTracker;

            _counter = counter;
            _counter.Counter = 0;
        }

        async public Task Render()
        {
            ILabelRenderer clonedLabelRenderer = null;

            try
            {
                if (_layer == null || _layer.LabelRenderer == null || _map == null)
                {
                    return;
                }

                IFeatureClass fClass = _layer.FeatureClass;
                if (fClass == null)
                {
                    return;
                }

                IGeometry filterGeom = _map.Display.Envelope;

                if (_map.Display.GeometricTransformer != null)
                {
                    filterGeom = MapHelper.Project(fClass, _map.Display);
                    //filterGeom = (IGeometry)_map.Display.GeometricTransformer.InvTransform2D(filterGeom);
                }

                SpatialFilter filter = new SpatialFilter();
                filter.DatasetCachingContext = _datasetCachingContext;
                filter.Geometry = filterGeom;
                filter.AddField(fClass.ShapeFieldName);
                filter.SpatialRelation = spatialRelation.SpatialRelationMapEnvelopeIntersects;

                if (_layer.FilterQuery != null)
                {
                    filter.WhereClause = _layer.FilterQuery.WhereClause;
                    if (_layer.FilterQuery is ISpatialFilter)
                    {
                        //filter.FuzzyQuery = ((ISpatialFilter)_layer.FilterQuery).FuzzyQuery;
                        filter.SpatialRelation = ((ISpatialFilter)_layer.FilterQuery).SpatialRelation;
                        filter.Geometry = ((ISpatialFilter)_layer.FilterQuery).Geometry;
                    }
                }

                #region Clone Layer

                ILabelRenderer labelRenderer = null;

                GraphicsEngine.Current.Engine?.CloneObjectsLocker.InterLock(() =>
                {
                    // Beim Clonen sprerren...
                    // Da sonst bei der Servicemap bei gleichzeitigen Requests
                    // Exception "Objekt wird bereits an anderer Stelle verwendet" auftreten kann!
                    //labelRenderer = (ILabelRenderer)_layer.LabelRenderer.Clone(new CloneOptions(_map, maxLabelRefscaleFactor: _layer.MaxLabelRefScaleFactor));

                    if (_layer.RequiresLabelRendererClone(_map))
                    {
                        labelRenderer = clonedLabelRenderer =
                            (ILabelRenderer)_layer.LabelRenderer.Clone(new CloneOptions(_map,
                                                                                        _layer.UseLabelsWithRefScale(_map),
                                                                                        maxLabelRefscaleFactor: _layer.MaxLabelRefScaleFactor));
                    }
                    else
                    {
                        labelRenderer = clonedLabelRenderer = (ILabelRenderer)_layer.LabelRenderer.Clone(null);
                    }
                });

                #endregion

                #region Prepare Filter

                labelRenderer.PrepareQueryFilter(_map.Display, _layer, filter);

                #endregion

                using (IFeatureCursor fCursor = await fClass.GetFeatures(filter))
                {
                    if (fCursor != null)
                    {

                        IFeature feature;

                        while ((feature = await fCursor.NextFeature()) != null)
                        {
                            if (_cancelTracker != null)
                            {
                                if (!_cancelTracker.Continue)
                                {
                                    break;
                                }
                            }

                            _counter.Counter++;
                            labelRenderer.Draw(_map, _layer, feature);
                        }

                        labelRenderer.Release();
                    }
                }
            }
            catch (Exception ex)
            {
                if (_map is IServiceMap && ((IServiceMap)_map).MapServer != null)
                {
                    await ((IServiceMap)_map).MapServer.LogAsync(
                        ((IServiceMap)_map).Name,
                        "RenderLabelThread:" + (_layer != null ? _layer.Title : string.Empty),
                        loggingMethod.error,
                        ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace);
                }
                if (_map != null)
                {
                    if (_map != null)
                    {
                        _map.AddRequestException(new Exception("RenderLabelThread: " + (_layer != null ? _layer.Title : string.Empty) + "\n" + ex.Message, ex));
                    }
                }
            }
            finally
            {
                if (clonedLabelRenderer != null)
                {
                    clonedLabelRenderer.Release();
                }
            }
        }
    }
}
