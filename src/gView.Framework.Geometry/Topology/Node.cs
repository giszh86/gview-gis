using gView.Framework.Core.Geometry;
using System.Collections.Generic;

namespace gView.Framework.Geometry.Topology;

public class Nodes : List<IPoint>
{
    new public void Add(IPoint p)
    {
        if (p == null || this.Contains(p))
        {
            return;
        }

        base.Add(p);
    }
    new public bool Contains(IPoint p)
    {
        foreach (IPoint point in this)
        {
            if (point.Equals(p, double.Epsilon))
            {
                return true;
            }
        }
        return false;
    }
}
