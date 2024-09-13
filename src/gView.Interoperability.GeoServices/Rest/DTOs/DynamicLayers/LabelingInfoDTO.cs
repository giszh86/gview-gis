﻿using gView.Framework.Cartography.Rendering;
using gView.Framework.Core.Carto;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.Symbology;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace gView.Interoperability.GeoServices.Rest.DTOs.DynamicLayers
{
    public class LabelingInfoDTO
    {
        [JsonPropertyName("labelPlacement")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LabelPlacement { get; set; }

        [JsonPropertyName("labelExpression")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LabelExpression { get; set; }

        [JsonPropertyName("useCodedValues")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? UseCodedValues { get; set; }

        [JsonPropertyName("symbol")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object Symbol { get; set; }

        [JsonPropertyName("minScale")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? MinScale { get; set; }

        [JsonPropertyName("maxScale")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? MaxScale { get; set; }

        [JsonPropertyName("where")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Where { get; set; }

        public static string DefaultLabelPlacement(GeometryType type)
        {
            switch (type)
            {
                case GeometryType.Point:
                    return "esriServerPointLabelPlacementAboveRight";
                case GeometryType.Polyline:
                    return "esriServerLinePlacementAboveAlong";
                case GeometryType.Polygon:
                    return "esriServerPolygonPlacementAlwaysHorizontal";
            }

            return String.Empty;
        }

        #region Members

        public ILabelRenderer ToLabelRenderer()
        {
            var labelRenderer = new SimpleLabelRenderer();

            if (!String.IsNullOrWhiteSpace(LabelExpression))
            {
                labelRenderer.UseExpression = true;
                labelRenderer.LabelExpression = LabelExpression;
            }
            labelRenderer.TextSymbol = this.Symbol switch
            {
                JsonElement jsonElement => Renderers.SimpleRenderers.JsonRendererDTO.FromSymbolJObject(jsonElement) as ITextSymbol,
                _ => null
            };

            return labelRenderer;
        }

        #endregion
    }
}

/*
 * 
Label Placement Values For Point Features
esriServerPointLabelPlacementAboveCenter 	esriServerPointLabelPlacementAboveLeft 	esriServerPointLabelPlacementAboveRight
esriServerPointLabelPlacementBelowCenter 	esriServerPointLabelPlacementBelowLeft 	esriServerPointLabelPlacementBelowRight
esriServerPointLabelPlacementCenterCenter 	esriServerPointLabelPlacementCenterLeft 	esriServerPointLabelPlacementCenterRight
 * 
Label Placement Values For Line Features
esriServerLinePlacementAboveAfter 	esriServerLinePlacementAboveAlong 	esriServerLinePlacementAboveBefore
esriServerLinePlacementAboveStart 	esriServerLinePlacementAboveEnd 	 
esriServerLinePlacementBelowAfter 	esriServerLinePlacementBelowAlong 	esriServerLinePlacementBelowBefore
esriServerLinePlacementBelowStart 	esriServerLinePlacementBelowEnd 	 
esriServerLinePlacementCenterAfter 	esriServerLinePlacementCenterAlong 	esriServerLinePlacementCenterBefore
esriServerLinePlacementCenterStart 	esriServerLinePlacementCenterEnd 	 
 * 
Label Placement Values For Polygon Features
esriServerPolygonPlacementAlwaysHorizontal
 
 * 
 * 
 *  Example
{
    "labelPlacement": "esriServerPointLabelPlacementAboveRight",
    "labelExpression": "[NAME]",
    "useCodedValues": false,
    "symbol": {
     "type": "esriTS",
     "color": [38,115,0,255],
     "backgroundColor": null,
     "borderLineColor": null,
     "verticalAlignment": "bottom",
     "horizontalAlignment": "left",
     "rightToLeft": false,
     "angle": 0,
     "xoffset": 0,
     "yoffset": 0,
     "kerning": true,
     "font": {
      "family": "Arial",
      "size": 11,
      "style": "normal",
      "weight": "bold",
      "decoration": "none"
     }
    },
    "minScale": 0,
    "maxScale": 0,
    "where" : "NAME LIKE 'A%'" //label only those feature where name begins with A
 } 
 * 
 */


