﻿using System.Collections.Generic;

namespace gView.Framework.Core.IO
{
    public interface IResourceContainer : IPersistable
    {
        IEnumerable<string> Names { get; }

        byte[] this[string name] { get; set; }

        bool HasResources { get; }
    }

    public interface IResource
    {
        string Name { get; }
        byte[] Data { get; }
    }
}
