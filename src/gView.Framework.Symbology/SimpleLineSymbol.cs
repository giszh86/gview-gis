﻿#nullable enable

using gView.Framework.Core.Carto;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.IO;
using gView.Framework.Core.Symbology;
using gView.Framework.Core.Common;
using gView.Framework.Geometry;
using gView.Framework.Geometry.GeoProcessing;
using gView.Framework.Symbology.UI;
using gView.Framework.Symbology.UI.Abstractions;
using gView.GraphicsEngine;
using gView.GraphicsEngine.Abstraction;
using System.ComponentModel;

namespace gView.Framework.Symbology
{
    [RegisterPlugIn("91CC3F6F-0EC5-42b7-AA34-9C89803118E7")]
    public sealed class SimpleLineSymbol : Symbol,
                        ILineSymbol,
                        IPenColor,
                        IPenWidth,
                        IPenDashStyle,
                        IQuickSymolPropertyProvider
    {
        private IPen? _pen;
        private ArgbColor _color;

        public SimpleLineSymbol()
        {
            _color = ArgbColor.Black;
            _pen = Current.Engine.CreatePen(_color, 1);
            _pen.LineJoin = LineJoin.Round;
        }

        private SimpleLineSymbol(ArgbColor color, float width)
        {
            _color = color;
            _pen = Current.Engine.CreatePen(_color, width);
            _pen.LineJoin = LineJoin.Round;
        }

        ~SimpleLineSymbol()
        {
            this.Release();
        }

        public override string ToString()
        {
            return this.Name;
        }

        [Browsable(true)]
        [UseDashStylePicker()]
        public LineDashStyle DashStyle
        {
            get
            {
                return _pen?.DashStyle ?? LineDashStyle.Solid;
            }
            set
            {
                if (_pen is not null)
                {
                    _pen.DashStyle = value;
                }
            }
        }

        public LineCap LineStartCap
        {
            get
            {
                return _pen?.StartCap ?? LineCap.Round;
            }
            set
            {
                if (_pen is not null)
                {
                    _pen.StartCap = value;
                }
            }
        }

        public LineCap LineEndCap
        {
            get
            {
                return _pen?.EndCap ?? LineCap.Round;
            }
            set
            {
                if (_pen is not null)
                {
                    _pen.EndCap = value;
                }
            }
        }

        [Browsable(true)]
        [UseWidthPicker()]
        public float Width
        {
            get
            {
                return _pen?.Width ?? 1f;
            }
            set
            {
                if (_pen == null)
                {
                    _pen = Current.Engine.CreatePen(_color, value);
                }
                else
                {
                    _pen.Width = value;
                }
            }
        }

        [UseColorPicker()]
        public ArgbColor Color
        {
            get
            {
                return _color;
            }
            set
            {
                if (_pen is not null)
                {
                    _pen.Color = value;
                }
                _color = value;
            }
        }

        #region ILineSymbol Member

        public void DrawPath(IDisplay display, IGraphicsPath path)
        {
            if (path != null)
            {
                display.Canvas.SmoothingMode = (SmoothingMode)this.Smoothingmode;
                display.Canvas.DrawPath(_pen, path);
                display.Canvas.SmoothingMode = SmoothingMode.None;
            }
        }

        #endregion

        #region ISymbol Member

        public bool SupportsGeometryType(GeometryType geomType) => geomType == GeometryType.Polyline;

        public void Draw(IDisplay display, IGeometry geometry)
        {
            // Wenn DashStyle nicht Solid (und Antialiasing) soll Geometry erst geclippt werden,
            // da es sonst zu extrem Zeitaufwendigen Graphikopertation kommt...

            if (this.DashStyle != LineDashStyle.Solid &&
                this.Smoothingmode != SymbolSmoothing.None)
            {
                IEnvelope dispEnvelope =
                    display.DisplayTransformation != null ?
                    new Envelope(display.DisplayTransformation.TransformedBounds(display)) :
                    new Envelope(display.Envelope);

                //dispEnvelope.Raise(75);
                geometry = Clip.PerformClip(dispEnvelope, geometry);
                if (geometry == null)
                {
                    return;
                }

                //GraphicsPath gp2 = DisplayOperations.Geometry2GraphicsPath(display, dispEnvelope);
                //if (gp2 != null)
                //{
                //    this.DrawPath(display, gp2);
                //    gp2.Dispose(); gp2 = null;
                //}
            }

            var gp = DisplayOperations.Geometry2GraphicsPath(display, geometry);
            if (gp != null)
            {
                //if (this.LineStartCap == LineCap.ArrowAnchor ||
                //    this.LineEndCap == LineCap.ArrowAnchor)
                //{
                //    //
                //    // bei LineCap Arrow (Pfeil...) kann es bei sehr kurzen Linen
                //    // zu einer Out of Memory Exception kommen...
                //    //
                //    try
                //    {
                //        this.DrawPath(display, gp);
                //    }
                //    catch
                //    {
                //        LineCap sCap = this.LineStartCap;
                //        LineCap eCap = this.LineEndCap;
                //        this.LineStartCap = (sCap == LineCap.ArrowAnchor) ? LineCap.Triangle : sCap;
                //        this.LineEndCap = (eCap == LineCap.ArrowAnchor) ? LineCap.Triangle : eCap;

                //        this.DrawPath(display, gp);

                //        this.LineStartCap = sCap;
                //        this.LineEndCap = eCap;
                //    }
                //}
                //else
                {
                    this.DrawPath(display, gp);
                }
                gp.Dispose(); gp = null;
            }
        }

        public void Release()
        {
            if (_pen != null)
            {
                _pen.Dispose();
                _pen = null;
            }
        }


        [Browsable(false)]
        public string Name
        {
            get
            {
                return "Line Symbol";
            }
        }

        #endregion

        #region IPersistable Member

        //[Browsable(false)]
        //public string PersistID
        //{
        //    get
        //    {
        //        return null;
        //    }
        //}

        new public void Load(IPersistStream stream)
        {
            base.Load(stream);

            this.Color = ArgbColor.FromArgb((int)stream.Load("color", ArgbColor.Black.ToArgb()));
            this.Width = (float)stream.Load("width", (float)1);
            this.DashStyle = (LineDashStyle)stream.Load("dashstyle", LineDashStyle.Solid);

            this.LineStartCap = (LineCap)stream.Load("linescap", LineCap.Flat);
            this.LineEndCap = (LineCap)stream.Load("lineecap", LineCap.Flat);

            int cap_old = (int)stream.Load("linecap", -1);
            if (cap_old >= 0)
            {
                this.LineStartCap = this.LineEndCap = (LineCap)cap_old;
            }

            this.MaxPenWidth = (float)stream.Load("maxwidth", 0f);
            this.MinPenWidth = (float)stream.Load("minwidth", 0f);
            this.PenWidthUnit = (DrawingUnit)stream.Load("widthunit", DrawingUnit.Pixel);
        }

        new public void Save(IPersistStream stream)
        {
            base.Save(stream);

            stream.Save("color", this.Color.ToArgb());
            stream.Save("width", this.Width);
            stream.Save("dashstyle", (int)this.DashStyle);
            stream.Save("linescap", (int)this.LineStartCap);
            stream.Save("lineecap", (int)this.LineEndCap);

            stream.Save("maxwidth", MaxPenWidth);
            stream.Save("minwidth", MinPenWidth);
            stream.Save("widthunit", (int)this.PenWidthUnit);
        }

        #endregion

        #region IClone2

        public object Clone(CloneOptions options)
        {
            var display = options?.Display;

            if (display == null)
            {
                return Clone();
            }

            float fac = 1;
            if (_widthUnit == DrawingUnit.Pixel)
            {
                fac = ReferenceScaleHelper.CalcPixelUnitFactor(options);
            }
            else if (_widthUnit != DrawingUnit.Pixel && _pen?.Width > 0)
            {
                float pix = (float)(display.MapScale / (display.Dpi / 0.0254));
                if (pix == 0f)
                {
                    fac = 0;
                }
                else
                {
                    fac = 1f / pix;
                }
            }

            SimpleLineSymbol clone = new SimpleLineSymbol(_color, ReferenceScaleHelper.PenWidth((_pen?.Width ?? 1f) * fac, this, display));
            clone.DashStyle = this.DashStyle;
            clone.LineStartCap = this.LineStartCap;
            clone.LineEndCap = this.LineEndCap;
            clone.Smoothingmode = this.Smoothingmode;
            clone.LegendLabel = _legendLabel;
            clone.MinPenWidth = _minWidth;
            clone.MaxPenWidth = _maxWidth;

            clone.PenWidthUnit = _widthUnit;

            return clone;
        }

        #endregion

        #region IPenColor Member

        [Browsable(false)]
        public ArgbColor PenColor
        {
            get
            {
                return Color;
            }
            set
            {
                Color = value;
            }
        }

        #endregion

        #region IPenWidth Member

        [Browsable(false)]
        public float PenWidth
        {
            get
            {
                return this.Width;
            }
            set
            {
                this.Width = value;
            }
        }

        private float _maxWidth, _minWidth;

        [Browsable(true)]
        [Category("Reference Scaling")]
        [UseWidthPicker()]
        public float MaxPenWidth
        {
            get
            {
                return _maxWidth;
            }
            set
            {
                _maxWidth = value;
            }
        }

        [Browsable(true)]
        [Category("Reference Scaling")]
        [UseWidthPicker()]
        public float MinPenWidth
        {
            get
            {
                return _minWidth;
            }
            set
            {
                _minWidth = value;
            }
        }

        private DrawingUnit _widthUnit;

        [Browsable(true)]
        [DisplayName("Width Unit")]
        [Category("Unit")]
        public DrawingUnit PenWidthUnit
        {
            get { return _widthUnit; }
            set { _widthUnit = value; }
        }

        #endregion

        #region IPenDashStyle Member

        [Browsable(false)]
        public LineDashStyle PenDashStyle
        {
            get
            {
                return this.DashStyle;
            }
            set
            {
                this.DashStyle = value;
            }
        }

        #endregion

        #region ISymbol Member

        [Browsable(false)]
        public SymbolSmoothing SymbolSmoothingMode
        {
            get => this.Smoothingmode;
            set { this.Smoothingmode = value; }
        }

        public bool RequireClone()
        {
            return _widthUnit != DrawingUnit.Pixel;
        }

        #endregion

        #region IQuickSymolPropertyProvider

        public IQuickSymbolProperties? GetQuickSymbolProperties()
        {
            return new QuickLineSymbolProperties(this);
        }

        #endregion
    }
}
