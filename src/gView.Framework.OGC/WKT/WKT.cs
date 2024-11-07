using gView.Framework.Core.Geometry;
using gView.Framework.Geometry.GeoProcessing;
using System.Collections.Generic;
using System.Text;

namespace gView.Framework.OGC.WKT
{
    public class WKT
    {
        public readonly static System.Globalization.NumberFormatInfo _nhi = new System.Globalization.CultureInfo("en-US", false).NumberFormat;

        public static string ToWKT(IGeometry geometry)
        {
            StringBuilder sb = new StringBuilder();
            if (geometry is IPoint)
            {
                sb.Append("POINT(");
                AppendPoint(sb, (IPoint)geometry);
                sb.Append(")");
            }
            else if (geometry is IMultiPoint)
            {
                sb.Append("MULTIPOINT(");
                bool first = true;
                for (int i = 0; i < ((IMultiPoint)geometry).PointCount; i++)
                {
                    IPoint mPoint = ((IMultiPoint)geometry)[i];
                    if (mPoint == null)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.Append(",");
                    }

                    sb.Append("(");
                    AppendPoint(sb, mPoint);
                    sb.Append(")");
                    first = false;
                }
                sb.Append(")");
            }
            else if (geometry is IPolyline)
            {
                sb.Append("MULTILINESTRING(");
                AppendPolyline(sb, (IPolyline)geometry);
                sb.Append(")");
            }
            else if (geometry is IPolygon)
            {
                switch (((IPolygon)geometry).RingCount)
                {
                    case 0:
                        sb.Append("MULTIPOLYGON EMPTY");
                        break;
                    case 1:
                        sb.Append("POLYGON(");
                        AppendPolygon(sb, (IPolygon)geometry);
                        sb.Append(")");
                        break;
                    default:
                        List<IPolygon> polygons = Algorithm.SplitPolygonToDonutsAndPolygons((IPolygon)geometry);
                        if (polygons == null)
                        {
                            sb.Append("MULTIPOLYGON EMPTY");
                            break;
                        }
                        if (polygons.Count == 1)
                        {
                            sb.Append("POLYGON(");
                            AppendPolygon(sb, polygons[0]);
                            sb.Append(")");
                        }
                        else
                        {
                            sb.Append("MULTIPOLYGON(");
                            bool first = true;

                            var equal = polygons[1].Equals(polygons[1]);

                            foreach (IPolygon mPoly in polygons)
                            {
                                if (!first)
                                {
                                    sb.Append(",");
                                }

                                sb.Append("(");
                                AppendPolygon(sb, mPoly);
                                sb.Append(")");

                                first = false;
                            }
                            sb.Append(")");
                        }
                        break;
                }
            }
            else if (geometry is IAggregateGeometry)
            {
                sb.Append("GEOMETRYCOLLECTION(");
                bool first = true;
                for (int i = 0; i < ((IAggregateGeometry)geometry).GeometryCount; i++)
                {
                    IGeometry aGeom = ((IAggregateGeometry)geometry)[i];
                    if (aGeom == null)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.Append(",");
                    }

                    sb.Append(ToWKT(aGeom));
                    first = false;
                }
                sb.Append(")");
            }
            else if (geometry is IEnvelope)
            {
                IEnvelope env = (IEnvelope)geometry;
                sb.Append("POLYGON((");
                sb.Append(env.MinX.ToString(_nhi) + " ");
                sb.Append(env.MinY.ToString(_nhi) + ",");

                sb.Append(env.MaxX.ToString(_nhi) + " ");
                sb.Append(env.MinY.ToString(_nhi) + ",");

                sb.Append(env.MaxX.ToString(_nhi) + " ");
                sb.Append(env.MaxY.ToString(_nhi) + ",");

                sb.Append(env.MinX.ToString(_nhi) + " ");
                sb.Append(env.MaxY.ToString(_nhi) + ",");

                sb.Append(env.MinX.ToString(_nhi) + " ");
                sb.Append(env.MinY.ToString(_nhi) + "))");
            }
            return sb.ToString();
        }

        private static void AppendPoint(StringBuilder sb, IPoint point)
        {
            if (point == null)
            {
                return;
            }

            sb.Append(point.X.ToString(_nhi) + " " + point.Y.ToString(_nhi));
        }

        private static void AppendPointCollection(StringBuilder sb, IPointCollection pColl)
        {
            if (pColl == null || pColl.PointCount == 0)
            {
                return;
            }

            sb.Append("(");
            bool first = true;
            for (int i = 0; i < pColl.PointCount; i++)
            {
                IPoint p = pColl[i];
                if (p != null)
                {
                    if (!first)
                    {
                        sb.Append(",");
                    }

                    AppendPoint(sb, p);
                    first = false;
                }
            }
            sb.Append(")");
        }

        private static void AppendPolyline(StringBuilder sb, IPolyline pLine)
        {
            if (pLine == null || pLine.PathCount == 0)
            {
                return;
            }

            bool first = true;
            for (int i = 0; i < pLine.PathCount; i++)
            {
                IPath p = pLine[i];
                if (p != null && p.PointCount > 1)
                {
                    if (!first)
                    {
                        sb.Append(",");
                    }

                    AppendPointCollection(sb, p);
                    first = false;
                }
            }
        }

        private static void AppendPolygon(StringBuilder sb, IPolygon poly)
        {
            if (poly == null || poly.RingCount == 0)
            {
                return;
            }

            bool first = true;
            for (int i = 0; i < poly.RingCount; i++)
            {
                IRing r = poly[i];
                if (r != null && r.PointCount > 2)
                {
                    if (!first)
                    {
                        sb.Append(",");
                    }

                    r.ClosePath();
                    AppendPointCollection(sb, r);
                    first = false;
                }
            }
        }
    }
}
