﻿using System.Collections.Generic;

namespace gView.Framework.Core.Geometry
{
    public interface IPolygon : IGeometry, IEnumerable<IRing>
    {
        void AddRing(IRing ring);
        void InsertRing(IRing ring, int pos);
        void RemoveRing(int pos);

        int RingCount { get; }
        IRing this[int ringIndex] { get; }

        void VerifyHoles();

        int OuterRingCount { get; }

        int InnerRingCount { get; }

        void CloseAllRings(double tolerance = GeometryConst.Epsilon);

        IEnumerable<IRing> OuterRings();
        IEnumerable<IHole> InnerRings(IRing outer);

        IEnumerable<IRing> Rings { get; }
        IEnumerable<IHole> Holes { get; }

        int TotalPointCount { get; }

        double Area { get; }

        double Distance2D(IPolygon p);
    }
}
