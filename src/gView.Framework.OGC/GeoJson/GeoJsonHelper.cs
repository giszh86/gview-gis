﻿using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Cursors;
using gView.Framework.Core.Geometry;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace gView.Framework.OGC.GeoJson
{
    public class GeoJsonHelper
    {
        private static IFormatProvider _nhi = System.Globalization.CultureInfo.InvariantCulture.NumberFormat;

        public static string ToGeoJson(IFeatureCursor cursor)
        {
            return ToGeoJson(cursor, int.MaxValue);
        }
        public static string ToGeoJson(IFeatureCursor cursor, int maxFeatures)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("{'type':'FeatureCollection','features':[");

            sb.Append(ToGeoJsonFeatures(cursor, maxFeatures));

            sb.Append("]}");

            return sb.ToString();
        }

        async public static Task<string> ToGeoJsonFeatures(IFeatureCursor cursor, int maxFeatures)
        {
            StringBuilder sb = new StringBuilder();

            int counter = 0;
            IFeature feature;
            while ((feature = await cursor.NextFeature()) != null)
            {
                if (counter != 0)
                {
                    sb.Append(",");
                }

                sb.Append(ToGeoJson(feature));
                counter++;
                if (counter >= maxFeatures)
                {
                    break;
                }
            }

            return sb.ToString();
        }

        public static string ToGeoJson(IEnumerable<IFeature> features)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(@"{""type"":""FeatureCollection"",""features"":[");

            sb.Append(ToGeoJsonFeatures(features));

            sb.Append("]}");

            return sb.ToString();
        }

        public static string ToGeoJsonFeatures(IEnumerable<IFeature> features)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var feature in features)
            {
                if (sb.Length > 0)
                {
                    sb.Append(",");
                }

                sb.Append(ToGeoJson(feature));
            }

            return sb.ToString();
        }

        public static string ToGeoJson(IFeature feature)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(@"{""type"":""Feature""");

            sb.Append(@",""properties"":{");
            for (int i = 0, to = feature.Fields.Count; i < to; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }

                FieldValue fv = feature.Fields[i];
                //sb.Append($"\"" + fv.Name + "\":\"" + fv.Value.ToString() + "\"");
                sb.Append($"\"{fv.Name}\":{JsonSerializer.Serialize(fv.Value)}");
            }
            sb.Append("}");

            if (feature.Shape != null)
            {
                sb.Append(@",""geometry"":");
                sb.Append(ToGeoJsonGeometry(feature.Shape));
            }

            sb.Append("}");

            return sb.ToString();
        }

        public static string ToGeoJsonGeometry(IGeometry geometry)
        {
            StringBuilder sb = new StringBuilder();

            if (geometry is IPoint)
            {
                sb.Append(@"{""type"":""Point"",""coordinates"":");
                sb.Append(ToGeoJsonPoint((IPoint)geometry));
                sb.Append("}");
            }
            else if (geometry is IMultiPoint)
            {
                sb.Append(@"{""type"":""MultiPoint"",""coordinates"":");
                sb.Append(ToGeoJsonPoints((IMultiPoint)geometry));
                sb.Append("}");
            }
            else if (geometry is IPolyline)
            {
                IPolyline pLine = (IPolyline)geometry;
                if (pLine.PathCount == 0)
                {
                    sb.Append(@"{""type"":""LineString"",""coordinates"":");
                    sb.Append("[]");
                    sb.Append("}");
                }
                else if (pLine.PathCount == 1)
                {
                    sb.Append(@"{""type"":""LineString"",""coordinates"":[");
                    sb.Append(ToGeoJsonPoints(pLine[0]));
                    sb.Append("]}");
                }
                else
                {
                    sb.Append(@"{""type"":""MultiLineString"",""coordinates"":[[");
                    for (int p = 0, to = pLine.PathCount; p < to; p++)
                    {
                        if (p > 0)
                        {
                            sb.Append("],[");
                        }

                        sb.Append(ToGeoJsonPoints(pLine[p]));
                    }
                    sb.Append("]]}");
                }
            }
            else if (geometry is IPolygon)
            {
                IPolygon polygon = (IPolygon)geometry;
                if (polygon.RingCount == 0)
                {
                    sb.Append(@"{""type"":""Polygon"",""coordinates"":");
                    sb.Append("[]");
                    sb.Append("}");
                }
                else if (polygon.RingCount == 1)
                {
                    sb.Append(@"{""type"":""Polygon"",""coordinates"":[");
                    sb.Append(ToGeoJsonPoints(polygon[0]));
                    sb.Append("]}");
                }
                else
                {
                    sb.Append(@"{""type"":""MultiPolygon"",""coordinates"":[[");
                    for (int r = 0, to = polygon.RingCount; r < to; r++)
                    {
                        if (r > 0)
                        {
                            sb.Append("],[");
                        }

                        sb.Append(ToGeoJsonPoints(polygon[r]));
                    }
                    sb.Append("]]}");
                }
            }
            else
            {
                sb.Append("{}");
            }

            return sb.ToString();
        }

        private static string ToGeoJsonPoint(IPoint point)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("[");
            sb.Append(point.X.ToString(_nhi));
            sb.Append(",");
            sb.Append(point.Y.ToString(_nhi));
            sb.Append("]");

            return sb.ToString();
        }

        private static string ToGeoJsonPoints(IPointCollection pColl)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("[");
            for (int i = 0, to = pColl.PointCount; i < to; i++)
            {
                if (i > 0)
                {
                    sb.Append(",");
                }

                sb.Append(ToGeoJsonPoint(pColl[i]));
            }
            sb.Append("]");

            return sb.ToString();
        }
    }
}
