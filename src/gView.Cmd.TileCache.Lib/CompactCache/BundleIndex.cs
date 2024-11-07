﻿using System;
using System.IO;

namespace gView.Cmd.TileCache.Lib.CompactCache;

class BundleIndex
{
    public BundleIndex(string filename)
    {
        this.Filename = filename;
    }

    public string Filename { get; private set; }

    public bool Exists
    {
        get
        {
            return new FileInfo(this.Filename).Exists;
        }
    }

    public int TilePosition(int row, int col, out int tileLength)
    {
        if (row < 0 || row > 128 || col < 0 || col > 128)
        {
            throw new ArgumentException("Compact Tile Index out of range");
        }

        int indexPosition = ((row * 128) + col) * 8;

        using (FileStream fs = new FileStream(this.Filename, FileMode.Open, FileAccess.Read))
        {
            byte[] data = new byte[8];
            fs.Position = indexPosition;
            fs.Read(data, 0, 8);

            int position = BitConverter.ToInt32(data, 0);
            tileLength = BitConverter.ToInt32(data, 4);

            return position;
        }
    }
}
