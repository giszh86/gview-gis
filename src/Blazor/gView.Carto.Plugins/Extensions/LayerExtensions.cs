﻿using gView.Framework.Core.Data;
using gView.Framework.Core.Geometry;

namespace gView.Carto.Plugins.Extensions;

internal static class LayerExtensions
{
    static public IEnumerable<ILayer> OrderLayersByGeometryType(this IEnumerable<ILayer> layers)
        => layers.OrderBy(l => l.LayerGeometryTypeOrder());

    static public int LayerGeometryTypeOrder(this ILayer layer)
        => layer.Class switch
        {
            IFeatureClass { GeometryType: GeometryType.Point } => 0,
            IFeatureClass { GeometryType: GeometryType.Multipoint } => 1,
            IFeatureClass { GeometryType: GeometryType.Polyline } => 2,
            IFeatureClass { GeometryType: GeometryType.Polygon } => 3,
            IFeatureClass { GeometryType: GeometryType.Envelope } => 4,
            IFeatureClass { GeometryType: GeometryType.Unknown } => 5,
            IRasterCatalogClass => 7,
            IRasterClass => 6,
            _ => 99
        };

    static public ILayer? FirstOrHigherIndexOfGeometryTypeOrder(this IEnumerable<ILayer>? layers, ILayer candidate)
    {
        if (layers != null)
        {
            int candidateOrder = candidate.LayerGeometryTypeOrder();

            foreach (var layer in layers)
            {
                if (layer is IGroupLayer)
                {
                    continue;
                }

                if (layer.LayerGeometryTypeOrder() >= candidateOrder)
                {
                    return layer;
                }
            }
        }

        return null;
    }
}
