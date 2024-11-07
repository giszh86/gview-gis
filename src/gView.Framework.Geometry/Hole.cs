﻿using System.Collections.Generic;
using gView.Framework.Core.Geometry;

namespace gView.Framework.Geometry
{
    public class Hole : Ring, IHole
    {
        public Hole()
            : base()
        {
        }
        public Hole(List<IPoint> points)
            : base(points)
        {
        }

        internal Hole(IRing ring)
            : base(ring)
        {
        }

        public override object Clone()
        {
            return new Hole(_points);
        }
    }
}
