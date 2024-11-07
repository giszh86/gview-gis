using System.Text;
using System.Threading.Tasks;
using gView.Framework.OGC.Extensions;
using gView.Framework.Core.Data;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.Extensions;
using gView.Framework.Core.Data.Cursors;

namespace gView.Framework.OGC.GML
{
    public class FeatureTranslator
    {
        #region Feature To GML
        public static void Feature2GML(IFeature feature, IFeatureClass fc, string fcID, StringBuilder sb, string srsName, IGeometricTransformer transformer, GmlVersion version)
        {
            if (feature == null || fc == null)
            {
                return;
            }

            sb.Append(@"
   <gml:featureMember>
      <gv:" + fcID + " gml:id=\"" + fcID + "." + feature.OID + "\">");

            // Shape
            IGeometry shape = (transformer != null) ? transformer.Transform2D(feature.Shape) as IGeometry : feature.Shape;
            if (shape != null)
            {
                sb.Append(@"
         <gml:boundedBy>");
                sb.Append(GeometryTranslator.Geometry2GML(shape.Envelope, srsName, version));
                sb.Append(@"
         </gml:boundedBy>");

                sb.Append(@"
         <gv:" + fc.ShapeFieldName.Replace("#", "") + ">");
                sb.Append(GeometryTranslator.Geometry2GML(shape, srsName, version));
                sb.Append(@"
         </gv:" + fc.ShapeFieldName.Replace("#", "") + ">");
            }

            // Fields
            foreach (FieldValue fv in feature.Fields)
            {
                if (fv.Name == fc.ShapeFieldName)
                {
                    continue;
                }

                
                sb.Append(@"
         <gv:" + fv.Name.ToValidXmlTag() + ">" + fv.Value?.ToString().XmlEncoded() + "</gv:" + fv.Name.ToValidXmlTag() + ">");
            }
            sb.Append(@"
      </gv:" + fcID + @">
   </gml:featureMember>");
        }

        async public static Task Features2GML(IFeatureCursor cursor, IFeatureClass fc, string fcID, StringBuilder sb, string srsName, IGeometricTransformer transformer, GmlVersion version)
        {
            await Features2GML(cursor, fc, fcID, sb, srsName, transformer, version, -1);
        }

        async public static Task Features2GML(IFeatureCursor cursor, IFeatureClass fc, string fcID, StringBuilder sb, string srsName, IGeometricTransformer transformer, GmlVersion version, int maxFeatures)
        {
            if (cursor == null || fc == null)
            {
                return;
            }

            int count = 0;
            IFeature feature = null;
            while ((feature = await cursor.NextFeature()) != null)
            {
                Feature2GML(feature, fc, fcID, sb, srsName, transformer, version);
                count++;
                if (maxFeatures > 0 && count > maxFeatures)
                {
                    break;
                }
            }
        }
        #endregion
    }
}
