﻿using gView.Framework.Core.Geometry;
using gView.Framework.Core.Symbology;
using gView.Framework.Core.Common;
using System;
using System.Collections.Generic;

namespace gView.Framework.Geometry
{
    public class DisplayPath : IDisplayPath
    {
        private float _chainage = 0.0f;
        private IAnnotationPolygonCollision _apc = null;
        private List<GraphicsEngine.CanvasPointF> _points = new List<GraphicsEngine.CanvasPointF>();

        private Geometry.Point GetBezierPoint(double t, Geometry.Point p0, Geometry.Point p1, Geometry.Point p2, Geometry.Point p3)
        {
            double cx = 3D * (p1.X - p0.X);
            double bx = 3D * (p2.X - p1.X) - cx;
            double ax = p3.X - p0.X - cx - bx;
            double cy = 3D * (p1.Y - p0.Y);
            double by = 3D * (p2.Y - p1.Y);
            double ay = p3.Y - p0.Y - cy - by;

            double tCubed = t * t * t;
            double tSquare = t * t;

            double x = (ax * tCubed) + (bx * tSquare) + (cx * t) + p0.X;
            double y = (ay * tCubed) + (by * tSquare) + (cy * t) + p0.Y;

            return new Geometry.Point(x, y);
        }

        public GraphicsEngine.CanvasPointF? this[int i]
        {
            get
            {
                if (i >= 0 && i < _points.Count)
                {
                    return _points[i];
                }
                return null;
            }
        }

        public void AddPoint(GraphicsEngine.CanvasPointF point)
        {
            _points.Add(point);
        }

        #region IDisplayPath Member

        public float Chainage
        {
            get
            {
                return _chainage;
            }
            set
            {
                _chainage = value;
            }
        }

        public IAnnotationPolygonCollision AnnotationPolygonCollision
        {
            get
            {
                return _apc;
            }
            set
            {
                _apc = value;
            }
        }

        public float Length
        {
            get
            {
                if (_points.Count < 2)
                {
                    return 0.0f;
                }

                float len = 0.0f;
                for (int i = 1; i < _points.Count; i++)
                {
                    len += (float)Math.Sqrt(
                        (_points[i - 1].X - _points[i].X) * (_points[i - 1].X - _points[i].X) +
                        (_points[i - 1].Y - _points[i].Y) * (_points[i - 1].Y - _points[i].Y));
                }
                return len;
            }
        }

        public GraphicsEngine.CanvasPointF? PointAt(float stat)
        {
            if (_points.Count == 0)
            {
                return null;
            }

            float Station = 0.0f, Station0 = 0.0f;
            float x1, y1, x2, y2;
            x1 = _points[0].X;
            y1 = _points[0].Y;
            for (int i = 1; i < _points.Count; i++)
            {
                x2 = _points[i].X;
                y2 = _points[i].Y;

                Station0 = Station;
                Station += (float)Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                if (Station >= stat)
                {
                    float t = stat - Station0;
                    float l = (float)Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                    float dx = (x2 - x1) / l, dy = (y2 - y1) / l;
                    return new GraphicsEngine.CanvasPointF(x1 + dx * t, y1 + dy * t);
                }

                x1 = x2; y1 = y2;
            }

            return null;
        }

        public void ChangeDirection()
        {
            _points = ListOperations<GraphicsEngine.CanvasPointF>.Swap(_points);
        }

        #endregion

        #region IGeometry Member

        public GeometryType GeometryType
        {
            get { return GeometryType.Unknown; }
        }

        public IEnvelope Envelope
        {
            get { return null; ; }
        }

        public void Serialize(System.IO.BinaryWriter w, IGeometryDef geomDef)
        {

        }

        public void Deserialize(System.IO.BinaryReader r, IGeometryDef geomDef)
        {

        }

        public bool Equals(object obj, double epsi)
        {
            return false;
        }

        public int? Srs { get; set; }

        public int VertexCount => _points == null ? 0 : _points.Count;

        public void Clean(CleanGemetryMethods methods, double tolerance = 1e-8)
        {
            // Nothing to do for displaypaths?
        }

        public bool IsEmpty() => false;

        #endregion

        #region ICloneable Member

        public object Clone()
        {
            return null;
        }

        #endregion
    }
}
