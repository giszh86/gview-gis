﻿using gView.Framework.Core.Geometry;
using gView.Framework.Core.Symbology;
using gView.Framework.Symbology;
using gView.GraphicsEngine;
using System;

namespace gView.Framework.Cartography.Rendering
{
    public class RendererFunctions
    {
        static internal Random r = new Random(DateTime.Now.Millisecond);
        static internal ArgbColor RandomColor(int alpah = 255)
            => ArgbColor.FromArgb(alpah, r.Next(255), r.Next(255), r.Next(255));

        static public ISymbol CreateStandardSymbol(GeometryType type,
                                                   int fillAlpha = 85,
                                                   float lineWidth = 1,
                                                   int pointSize = 5)
        {
            ISymbol symbol = null;
            switch (type)
            {
                case GeometryType.Envelope:
                case GeometryType.Polygon:
                    symbol = new SimpleFillSymbol();
                    var fillColor = RandomColor(fillAlpha);
                    ((SimpleFillSymbol)symbol).Color = fillColor;
                    if (lineWidth >= 0)
                    {
                        ((SimpleFillSymbol)symbol).OutlineSymbol = new SimpleLineSymbol();
                        ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Color = ArgbColor.FromArgb(255, fillColor);
                        ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Width = lineWidth;
                    }
                    break;
                case GeometryType.Polyline:
                    symbol = new SimpleLineSymbol();
                    ((SimpleLineSymbol)symbol).Color = RandomColor();
                    ((SimpleLineSymbol)symbol).Width = lineWidth;
                    break;
                case GeometryType.Multipoint:
                case GeometryType.Point:
                    symbol = new SimplePointSymbol();
                    ((SimplePointSymbol)symbol).Color = RandomColor();
                    ((SimplePointSymbol)symbol).Size = pointSize;
                    ((SimplePointSymbol)symbol).SymbolWidth = lineWidth;
                    break;
            }

            if (symbol is not null)
            {
                symbol.SymbolSmoothingMode = SymbolSmoothing.AntiAlias;
            }

            return symbol;
        }
        static public ISymbol CreateStandardSelectionSymbol(GeometryType type)
        {
            ISymbol symbol = null;
            switch (type)
            {
                case GeometryType.Envelope:
                case GeometryType.Polygon:
                    symbol = new SimpleFillSymbol();
                    ((SimpleFillSymbol)symbol).Color = ArgbColor.FromArgb(85, ArgbColor.Cyan);
                    ((SimpleFillSymbol)symbol).SmoothingMode = SymbolSmoothing.AntiAlias;
                    ((SimpleFillSymbol)symbol).OutlineSymbol = new SimpleLineSymbol();
                    ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Color = ArgbColor.Cyan;
                    ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Width = 3;
                    ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Smoothingmode = SymbolSmoothing.AntiAlias;
                    break;
                case GeometryType.Polyline:
                    symbol = new SimpleLineSymbol();
                    ((SimpleLineSymbol)symbol).Color = ArgbColor.Cyan;
                    ((SimpleLineSymbol)symbol).Width = 5;
                    ((SimpleLineSymbol)symbol).Smoothingmode = SymbolSmoothing.AntiAlias;
                    break;
                case GeometryType.Point:
                    symbol = new SimplePointSymbol();
                    ((SimplePointSymbol)symbol).Color = ArgbColor.Cyan;
                    ((SimplePointSymbol)symbol).OutlineColor = ArgbColor.Cyan;
                    ((SimplePointSymbol)symbol).Size = 10;
                    ((SimplePointSymbol)symbol).Smoothingmode = SymbolSmoothing.AntiAlias;
                    break;
            }
            return symbol;
        }

        static public ISymbol CreateStandardHighlightSymbol(GeometryType type)
        {
            ISymbol symbol = null;
            switch (type)
            {
                case GeometryType.Envelope:
                case GeometryType.Polygon:
                    symbol = new SimpleFillSymbol();
                    ((SimpleFillSymbol)symbol).Color = ArgbColor.FromArgb(100, 255, 255, 0);
                    ((SimpleFillSymbol)symbol).SmoothingMode = SymbolSmoothing.AntiAlias;
                    ((SimpleFillSymbol)symbol).OutlineSymbol = new SimpleLineSymbol();
                    ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Color = ArgbColor.FromArgb(150, 255, 255, 0);
                    ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Width = 3;
                    ((SimpleLineSymbol)((SimpleFillSymbol)symbol).OutlineSymbol).Smoothingmode = SymbolSmoothing.AntiAlias;
                    break;
                case GeometryType.Polyline:
                    symbol = new SimpleLineSymbol();
                    ((SimpleLineSymbol)symbol).Color = ArgbColor.FromArgb(150, 255, 255, 0);
                    ((SimpleLineSymbol)symbol).Width = 5;
                    ((SimpleLineSymbol)symbol).Smoothingmode = SymbolSmoothing.AntiAlias;
                    break;
                case GeometryType.Point:
                    symbol = new SimplePointSymbol();
                    ((SimplePointSymbol)symbol).Color = ArgbColor.FromArgb(150, 255, 255, 0);
                    ((SimplePointSymbol)symbol).Size = 10;
                    ((SimplePointSymbol)symbol).Smoothingmode = SymbolSmoothing.AntiAlias;
                    break;
            }
            return symbol;
        }
    }
}
