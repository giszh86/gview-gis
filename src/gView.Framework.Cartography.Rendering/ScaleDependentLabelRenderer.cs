using gView.Framework.Cartography.Rendering.Abstractions;
using gView.Framework.Common;
using gView.Framework.Core.Carto;
using gView.Framework.Core.Common;
using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Filters;
using gView.Framework.Core.IO;
using gView.Framework.Core.Symbology;
using gView.Framework.Core.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace gView.Framework.Cartography.Rendering
{
    [RegisterPlugIn("4221EF57-E89E-4035-84EB-D3FA163FDE0C")]
    public class ScaleDependentLabelRenderer :
        ILabelRenderer,
        IGroupRenderer,
        ILegendGroup,
        IDefault
    {
        private RendererList _renderers;

        public ScaleDependentLabelRenderer()
        {
            _renderers = new RendererList();
        }

        #region IGroupRenderer Member

        public IRendererGroup RendererItems
        {
            get { return _renderers; }
        }

        #endregion

        #region ILabelRenderer Member

        public void PrepareQueryFilter(IDisplay display, IFeatureLayer layer, IQueryFilter filter)
        {
            foreach (ILabelRenderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.PrepareQueryFilter(display, layer, filter);
            }
        }

        public bool CanRender(IFeatureLayer layer, IMap map)
        {
            return true;
        }

        public string Name
        {
            get { return "Scale Dependent/Group Labelrenderer"; }
        }

        public LabelRenderMode RenderMode
        {
            get
            {
                return LabelRenderMode.UseRenderPriority;
            }
        }

        public int RenderPriority
        {
            get { return 0; }
        }

        public void Draw(IDisplay disp, IFeatureLayer layer, IFeature feature)
        {
            foreach (ILabelRenderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.Draw(disp, layer, feature);
            }
        }

        #endregion

        #region IPersistable Member

        public void Load(IPersistStream stream)
        {
            ScaleRendererPersist persist;
            while ((persist = stream.Load("ScaleRenderer", null, new ScaleRendererPersist(new ScaleRenderer(null))) as ScaleRendererPersist) != null)
            {
                _renderers.Add(persist.ScaleRenderer);
            }
        }

        public void Save(IPersistStream stream)
        {
            foreach (ILabelRenderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                stream.Save("ScaleRenderer", new ScaleRendererPersist(renderer as ScaleRenderer));
            }
        }
        #endregion

        #region IClone Member

        public object Clone()
        {
            ScaleDependentLabelRenderer scaleDependentRenderer = new ScaleDependentLabelRenderer();

            foreach (ILabelRenderer renderer in RendererItems)
            {
                scaleDependentRenderer.RendererItems.Add(renderer.Clone() as ScaleRenderer);
            }

            return scaleDependentRenderer;
        }

        #endregion

        #region IClone2 Member

        public object Clone(CloneOptions options)
        {
            ScaleDependentLabelRenderer scaledependentRenderer = new ScaleDependentLabelRenderer();
            foreach (ILabelRenderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                scaledependentRenderer._renderers.Add(renderer.Clone(options) as ScaleRenderer);
            }

            return scaledependentRenderer;
        }

        public void Release()
        {
            foreach (ILabelRenderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.Release();
            }
            _renderers.Clear();
        }

        #endregion

        #region ICreateDefault Member

        public ValueTask DefaultIfEmpty(object initObject)
        {
            return ValueTask.CompletedTask;
        }

        #endregion

        #region ILegendGroup Member

        public int LegendItemCount
        {
            get
            {
                int count = 0;
                foreach (ILabelRenderer renderer in _renderers)
                {
                    if (!(renderer is ILegendGroup))
                    {
                        continue;
                    }

                    count += ((ILegendGroup)renderer).LegendItemCount;
                }
                return count;
            }
        }

        public ILegendItem LegendItem(int index)
        {
            int count = 0;
            foreach (ILabelRenderer renderer in _renderers)
            {
                if (!(renderer is ILegendGroup))
                {
                    continue;
                }

                if (count + ((ILegendGroup)renderer).LegendItemCount > index)
                {
                    return ((ILegendGroup)renderer).LegendItem(index - count);
                }
                count += ((ILegendGroup)renderer).LegendItemCount;
            }
            return null;
        }

        public void SetSymbol(ILegendItem item, ISymbol symbol)
        {
            foreach (ILabelRenderer renderer in _renderers)
            {
                if (!(renderer is ILegendGroup))
                {
                    continue;
                }

                int count = ((ILegendGroup)renderer).LegendItemCount;
                for (int i = 0; i < count; i++)
                {
                    if (((ILegendGroup)renderer).LegendItem(i) == item)
                    {
                        ((ILegendGroup)renderer).SetSymbol(item, symbol);
                        return;
                    }
                }
            }
        }

        #endregion

        private class RendererList : List<IRendererGroupItem>, IRendererGroup
        {
            public new void Add(IRendererGroupItem item)
            {
                if (item == null)
                {
                    return;
                }

                if (item is ScaleRenderer)
                {
                    base.Add(item);
                }
                else if (item.Renderer is ILabelRenderer labelRenderer)
                {
                    base.Add(new ScaleRenderer(labelRenderer));
                }
            }

            public new void Clear()
            {
                foreach (var item in this)
                {
                    item.Renderer?.Release();
                }

                base.Clear();
            }

            public IRendererGroupItem Create(IRenderer renderer)
            => renderer is ILabelRenderer labelRenderer
                ? new ScaleRenderer(labelRenderer)
                : throw new ArgumentException("Renderer is not a label renderer");
        }

        private class ScaleRendererPersist : IPersistable
        {
            ScaleRenderer _renderer;
            public ScaleRendererPersist(ScaleRenderer scaleRenderer)
            {
                _renderer = scaleRenderer;
            }

            internal ScaleRenderer ScaleRenderer
            {
                get { return _renderer; }
            }

            #region IPersistable Member

            public void Load(IPersistStream stream)
            {
                if (_renderer == null)
                {
                    return;
                }

                _renderer.MinimumScale = (double)stream.Load("MinScale", 0.0);
                _renderer.MaximumScale = (double)stream.Load("MaxScale", 0.0);

                _renderer.Renderer = stream.Load("Renderer", null) as ILabelRenderer;
            }

            public void Save(IPersistStream stream)
            {
                if (_renderer == null)
                {
                    return;
                }

                stream.Save("MinScale", _renderer.MinimumScale);
                stream.Save("MaxScale", _renderer.MaximumScale);

                stream.Save("Renderer", _renderer.Renderer);
            }

            #endregion
        }

        private class ScaleRenderer :
            ILabelRenderer,
            ILegendGroup,
            IScaledependent,
            IRendererGroupItem
        {
            private ILabelRenderer _renderer = null;
            private double _minScale = 0, _maxScale = 0;

            public ScaleRenderer(ILabelRenderer renderer)
            {
                _renderer = renderer;
            }
            public ScaleRenderer(ILabelRenderer renderer, double minScale, double maxScale)
                : this(renderer)
            {
                _minScale = minScale;
                _maxScale = maxScale;
            }

            public IRenderer Renderer
            {
                get { return _renderer; }
                set
                {
                    if (value is ILabelRenderer labelRenderer)
                    {
                        _renderer = labelRenderer;
                    }
                    else
                    {
                        throw new ArgumentException("Renderer is not a label renderer");
                    }
                }
            }

            #region IScaledependent
            public double MinimumScale
            {
                get { return _minScale; }
                set { _minScale = value; }
            }

            public double MaximumScale
            {
                get { return _maxScale; }
                set { _maxScale = value; }
            }
            #endregion

            #region ILabelRenderer Member

            public void PrepareQueryFilter(IDisplay display, IFeatureLayer layer, IQueryFilter filter)
            {
                if (_renderer != null)
                {
                    _renderer.PrepareQueryFilter(display, layer, filter);
                }
            }

            public bool CanRender(IFeatureLayer layer, IMap map)
            {
                if (_renderer != null)
                {
                    return _renderer.CanRender(layer, map);
                }

                return false;
            }

            public string Name
            {
                get
                {
                    if (_renderer != null)
                    {
                        return _renderer.Name;
                    }

                    return "???";
                }
            }

            public LabelRenderMode RenderMode
            {
                get
                {
                    if (_renderer != null)
                    {
                        return _renderer.RenderMode;
                    }

                    return LabelRenderMode.UseRenderPriority;
                }
            }

            public int RenderPriority
            {
                get
                {
                    if (_renderer != null)
                    {
                        return _renderer.RenderPriority;
                    }

                    return 0;
                }
            }

            public void Draw(IDisplay disp, IFeatureLayer layer, IFeature feature)
            {
                if (_renderer == null)
                {
                    return;
                }

                if (MinimumScale > 1 && MinimumScale > disp.MapScale)
                {
                    return;
                }

                if (MaximumScale > 1 && MaximumScale < disp.MapScale)
                {
                    return;
                }

                _renderer.Draw(disp, layer, feature);
            }

            #endregion

            #region IgViewExtension Member

            public Guid GUID
            {
                get
                {
                    return PlugInManager.PlugInID(_renderer);
                }
            }

            #endregion

            #region IPersistable Member

            public void Load(IPersistStream stream)
            {
                ScaleRendererPersist persist = new ScaleRendererPersist(this);

                persist.Load(stream);
            }

            public void Save(IPersistStream stream)
            {
                ScaleRendererPersist persist = new ScaleRendererPersist(this);

                persist.Save(stream);
            }

            #endregion

            #region IClone Member

            public object Clone()
            {
                ILabelRenderer renderer = _renderer.Clone() as ILabelRenderer;

                ScaleRenderer scaleRenderer = new ScaleRenderer(renderer);
                scaleRenderer._minScale = _minScale;
                scaleRenderer._maxScale = _maxScale;
                return scaleRenderer;
            }

            #endregion

            #region IClone2 Member

            public object Clone(CloneOptions options)
            {
                if (_renderer == null)
                {
                    return null;
                }

                ILabelRenderer renderer = _renderer.Clone(options) as ILabelRenderer;
                if (renderer == null)
                {
                    return null;
                }

                ScaleRenderer scaleRenderer = new ScaleRenderer(renderer);
                scaleRenderer._minScale = _minScale;
                scaleRenderer._maxScale = _maxScale;

                return scaleRenderer;
            }

            public void Release()
            {
                if (_renderer != null)
                {
                    _renderer.Release();
                }
            }

            #endregion

            #region IPropertyPage Member

            public object PropertyPage(object initObject)
            {
                if (_renderer is IPropertyPage)
                {
                    return ((IPropertyPage)_renderer).PropertyPage(initObject);
                }

                return null;
            }

            public object PropertyPageObject()
            {
                if (_renderer is IPropertyPage)
                {
                    return ((IPropertyPage)_renderer).PropertyPageObject();
                }

                return null;
            }


            #endregion

            #region ILegendGroup Member

            public int LegendItemCount
            {
                get
                {
                    if (_renderer is ILegendGroup)
                    {
                        return ((ILegendGroup)_renderer).LegendItemCount;
                    }

                    return 0;
                }
            }

            public ILegendItem LegendItem(int index)
            {
                if (_renderer is ILegendGroup)
                {
                    return ((ILegendGroup)_renderer).LegendItem(index);
                }

                return null;
            }

            public void SetSymbol(ILegendItem item, ISymbol symbol)
            {
                if (_renderer is ILegendGroup)
                {
                    ((ILegendGroup)_renderer).SetSymbol(item, symbol);
                }
            }

            #endregion

            #region IRenderer Member

            public List<ISymbol> Symbols
            {
                get
                {
                    if (_renderer != null)
                    {
                        return _renderer.Symbols;
                    }

                    return new List<ISymbol>();
                }
            }

            public bool Combine(IRenderer renderer)
            {
                return false;
            }

            #endregion
        }

        #region IRenderer Member

        public List<ISymbol> Symbols
        {
            get
            {
                List<ISymbol> symbols = new List<ISymbol>();

                if (_renderers != null)
                {
                    foreach (IRenderer renderer in _renderers)
                    {
                        if (renderer == null)
                        {
                            continue;
                        }

                        symbols.AddRange(renderer.Symbols);
                    }
                }

                return symbols;
            }
        }

        public bool Combine(IRenderer renderer)
        {
            return false;
        }

        #endregion
    }
}
