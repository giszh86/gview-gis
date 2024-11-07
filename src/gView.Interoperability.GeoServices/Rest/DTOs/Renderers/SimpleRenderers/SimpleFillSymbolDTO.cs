﻿using System.Text.Json.Serialization;

namespace gView.Interoperability.GeoServices.Rest.DTOs.Renderers.SimpleRenderers
{
    public class SimpleFillSymbolDTO
    {
        public SimpleFillSymbolDTO()
        {
            this.Type = "esriSFS";
        }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("style")]
        public string Style { get; set; }

        [JsonPropertyName("color")]
        public int[] Color { get; set; }

        [JsonPropertyName("outline")]
        public SimpleLineSymbolDTO Outline { get; set; }
    }

    /*
    Simple Fill Symbol
    Simple fill symbols can be used to symbolize polygon geometries. The type property for simple fill symbols is esriSFS.
    
    JSON Syntax
    {
    "type" : "esriSFS",
    "style" : "< esriSFSBackwardDiagonal | esriSFSCross | esriSFSDiagonalCross | esriSFSForwardDiagonal | esriSFSHorizontal | esriSFSNull | esriSFSSolid | esriSFSVertical >",
    "color" : <color>,
    "outline" : <simpleLineSymbol> //if outline has been specified
    }

    JSON Example
    {
      "type": "esriSFS",
      "style": "esriSFSSolid",
      "color": [115,76,0,255],
        "outline": {
         "type": "esriSLS",
         "style": "esriSLSSolid",
         "color": [110,110,110,255],
         "width": 1
	     }
    }
    */
}
