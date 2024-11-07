﻿using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Cursors;
using gView.Framework.Core.Data.Filters;
using gView.Framework.Core.Geometry;
using gView.Framework.Data;
using gView.Framework.Data.Filters;
using gView.Framework.Geometry;
using System;
using System.Threading.Tasks;
using gView.Framework.Common.Extensions;

namespace gView.DataSources.GeoJson
{
    class GeoJsonServiceFeatureClass : IFeatureClass
    {
        private readonly GeoJsonServiceDataset _dataset;

        private GeoJsonServiceFeatureClass(GeoJsonServiceDataset dataset)
        {
            _dataset = dataset;


        }

        async public static Task<GeoJsonServiceFeatureClass> CreateInstance(GeoJsonServiceDataset dataset, GeometryType geometryType)
        {
            var instance = new GeoJsonServiceFeatureClass(dataset);
            instance.SpatialReference = await dataset.GetSpatialReference();
            instance.GeometryType = geometryType;
            instance.Name = $"{dataset.DatasetName}-{geometryType.ToString().ToLower()}";

            #region Loop all features for Fields

            var fields = new FieldCollection();
            if (dataset.Source != null)
            {
                bool idIsInterger = false;
                var features = await dataset.Source.GetFeatures(geometryType);

                // mayby later...
                //if (features != null && features.Count() > 0)
                //{
                //    idIsInterger = features.Where(f => f != null && int.TryParse(f["id"]?.ToString(), out int intId))
                //                           .Count() == features.Count();
                //}

                foreach (var feature in features)
                {
                    foreach (var fieldValue in feature.Fields)
                    {
                        if (fields.FindField(fieldValue.Name) == null)
                        {
                            var val = fieldValue.Value;
                            if (fieldValue.Name == "id" && idIsInterger)
                            {
                                fields.Add(new Field(fieldValue.Name, FieldType.ID));
                            }
                            else if (val?.GetType() == typeof(int))
                            {
                                fields.Add(new Field(fieldValue.Name, FieldType.integer));
                            }
                            else if (val?.GetType() == typeof(double))
                            {
                                fields.Add(new Field(fieldValue.Name, FieldType.Double));
                            }
                            else
                            {
                                // ToDo: Check for Date?
                                fields.Add(new Field(fieldValue.Name, FieldType.String));
                            }
                        }
                    }
                }
            }
            instance.Fields = fields;

            #endregion

            return instance;
        }

        #region IFeatureClass

        public string ShapeFieldName => "geometry";

        public IEnvelope Envelope => _dataset.Source?.Envelope ?? new Envelope();

        public IFieldCollection Fields { get; private set; }

        public string IDFieldName => "id";

        public string Name { get; private set; }

        public string Aliasname => this.Name;

        public IDataset Dataset => _dataset;

        public bool HasZ => false;

        public bool HasM => false;

        public ISpatialReference SpatialReference { get; internal set; }

        public GeometryType GeometryType { get; private set; }

        public Task<int> CountFeatures()
        {
            throw new NotImplementedException();
        }

        public IField FindField(string name)
        {
            return Fields.FindField(name);
        }

        async public Task<IFeatureCursor> GetFeatures(IQueryFilter filter)
        {
            if (filter is ISpatialFilter)
            {
                if (this.SpatialReference != null &&
                    ((ISpatialFilter)filter).FilterSpatialReference != null &&
                    !((ISpatialFilter)filter).FilterSpatialReference.Equals(this.SpatialReference))
                {
                    filter = (ISpatialFilter)filter.Clone();

                    ((ISpatialFilter)filter).Geometry =
                       GeometricTransformerFactory.Transform2D(((ISpatialFilter)filter).Geometry,
                        ((ISpatialFilter)filter).FilterSpatialReference,
                        this.SpatialReference);
                    ((ISpatialFilter)filter).FilterSpatialReference = null;
                }
            }

            if (_dataset.Source != null)
            {
                if (_dataset.Source.IsValid)
                {
                    if (filter is DistinctFilter)
                    {
                        return new GeoJsonDistinctFeatureCursor(await _dataset.Source?.GetFeatures(this.GeometryType), (DistinctFilter)filter);
                    }

                    return new GeoJsonFeatureCursor(this, await _dataset.Source?.GetFeatures(this.GeometryType), filter);
                }
                else if (_dataset.Source.LastException != null)
                {
                    throw new Exception(_dataset.Source.LastException.AllMessages());
                }
            }

            // Dataset is not intializalized
            return new GeoJsonFeatureCursor(this, new IFeature[0], filter);
        }

        async public Task<ICursor> Search(IQueryFilter filter)
        {
            return (ICursor)await GetFeatures(filter);
        }

        public Task<ISelectionSet> Select(IQueryFilter filter)
        {
            return Task.FromResult<ISelectionSet>(null);
        }

        #endregion
    }
}
