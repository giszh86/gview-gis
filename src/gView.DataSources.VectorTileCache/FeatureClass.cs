﻿using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Cursors;
using gView.Framework.Core.Data.Filters;
using gView.Framework.Core.Geometry;
using gView.Framework.Data;
using gView.Framework.Geometry;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace gView.DataSources.VectorTileCache
{
    class FeatureClass : IFeatureClass
    {
        public FeatureClass(Dataset dataset, string name)
        {
            this.Dataset = dataset;
            this.Name = name;

            if (dataset.Capabilities == null)
            {
                throw new ArgumentException("dataset is not intialized");
            }

            var layer = dataset.Capabilities.VectorLayers?
                    .Where(l => name.Equals(l.Id))
                    .FirstOrDefault();

            if (layer == null)
            {
                throw new Exception($"layer {name} is not included in tile cache capabilities");
            }

            if (dataset.Capabilities.Bounds != null && dataset.Capabilities.Bounds.Length == 4)
            {
                this.Envelope = new Envelope(
                    dataset.Capabilities.Bounds[0],
                    dataset.Capabilities.Bounds[1],
                    dataset.Capabilities.Bounds[2],
                    dataset.Capabilities.Bounds[3]);
            }

            this.Fields = new FieldCollection();
            if (layer.Fields != null)
            {
                foreach (var fieldName in layer.Fields.Keys)
                {
                    var field = new Field() { name = fieldName };

                    switch (layer.Fields[fieldName]?.ToLower())
                    {
                        case "number":
                            field.type = FieldType.Double;
                            break;
                        default:
                            field.type = FieldType.String;
                            break;
                    }

                    ((FieldCollection)this.Fields).Add(field);
                }
            }


            this.SpatialReference = dataset.SpatialReference;
        }

        #region IFeatureClass
        public string ShapeFieldName => "geometry";

        public IEnvelope Envelope { get; }

        public IFieldCollection Fields { get; }

        public string IDFieldName => "id"; // ???

        public string Name { get; }

        public string Aliasname => this.Name;

        public IDataset Dataset { get; }

        public bool HasZ => false;

        public bool HasM => false;

        public ISpatialReference SpatialReference { get; }

        public GeometryType GeometryType => GeometryType.Unknown;

        public Task<int> CountFeatures()
        {
            return Task.FromResult(int.MaxValue);
        }

        public IField FindField(string name)
        {
            return this.Fields?.FindField(name);
        }

        public Task<IFeatureCursor> GetFeatures(IQueryFilter filter)
        {
            return Task.FromResult<IFeatureCursor>(new FeatureCursor(this, filter));
        }

        public Task<ICursor> Search(IQueryFilter filter)
        {
            throw new NotImplementedException();
        }

        public Task<ISelectionSet> Select(IQueryFilter filter)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
