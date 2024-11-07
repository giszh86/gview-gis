﻿namespace gView.Framework.Core.Symbology
{
    public enum SymbolSmoothing
    {
        None = GraphicsEngine.SmoothingMode.None,
        AntiAlias = GraphicsEngine.SmoothingMode.AntiAlias
    }

    public enum TextSymbolAlignment { rightAlignOver, Over, leftAlignOver, rightAlignCenter, Center, leftAlignCenter, rightAlignUnder, Under, leftAlignUnder }

    public enum RotationType { geographic, arithmetic }
    public enum RotationUnit { rad, deg, gon }

    // Never Change values!!
    public enum DrawingUnit
    {
        Pixel = 0,
        Meters = 1
    }
}
