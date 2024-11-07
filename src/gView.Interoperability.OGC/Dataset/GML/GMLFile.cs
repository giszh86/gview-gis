using gView.Framework.Core.Data;
using gView.Framework.Core.Geometry;
using gView.Framework.Data;
using gView.Framework.Geometry;
using gView.Framework.OGC.GML;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace gView.Interoperability.OGC.Dataset.GML
{
    internal class GMLFile
    {
        private Dataset _gmlDataset = null;
        private string _filename = String.Empty, _errMsg = String.Empty;
        private XmlDocument _doc = null;
        private XmlNamespaceManager _ns = null;
        private static IFormatProvider _nhi = System.Globalization.CultureInfo.InvariantCulture.NumberFormat;
        private GmlVersion _gmlVersion = GmlVersion.v1;

        private GMLFile() { }

        async static public Task<GMLFile> Create(string filename)
        {
            try
            {
                var file = new GMLFile();

                file._gmlDataset = new Dataset();
                await file._gmlDataset.SetConnectionString(filename);
                if (!await file._gmlDataset.Open())
                {
                    file._gmlDataset = null;
                }

                file._filename = filename;

                file._doc = new XmlDocument();
                file._doc.Load(file._filename);

                file._ns = new XmlNamespaceManager(file._doc.NameTable);
                file._ns.AddNamespace("GML", "http://www.opengis.net/gml");
                file._ns.AddNamespace("WFS", "http://www.opengis.net/wfs");
                file._ns.AddNamespace("OGC", "http://www.opengis.net/ogc");
                file._ns.AddNamespace("myns", file._gmlDataset.targetNamespace);

                return file;
            }
            catch
            {
                return null;
            }
        }

        public bool Delete()
        {
            if (_gmlDataset != null)
            {
                return _gmlDataset.Delete();
            }

            return false;
        }

        async public static Task<bool> Create(string filename, IGeometryDef geomDef, FieldCollection fields, GmlVersion gmlVersion)
        {
            try
            {
                FileInfo fi = new FileInfo(filename);
                string name = fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length);

                string gml_filename = fi.FullName.Substring(0, fi.FullName.Length - fi.Extension.Length) + ".gml";
                string xsd_filename = fi.FullName.Substring(0, fi.FullName.Length - fi.Extension.Length) + ".xsd";

                FeatureClass featureClass = await FeatureClass.CreateAsync(null, name, fields);
                XmlSchemaWriter schemaWriter = new XmlSchemaWriter(featureClass);
                string schema = schemaWriter.Write();

                using (var sw = new StreamWriter(xsd_filename, false, Encoding.UTF8))
                {
                    sw.WriteLine(schema.Trim());
                }

                using (var sw = new StreamWriter(gml_filename, false, Encoding.UTF8))
                {
                    sw.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<gml:FeatureCollection xmlns:gml=""http://www.opengis.net/gml"" 
                       xmlns:xlink=""http://www.w3.org/1999/xlink"" 
                       xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                       xmlns:gv=""http://www.gViewGIS.com/server"" 
                       xsi:schemaLocation=""http://www.gview.com/gml " + name + @".xsd"">".Trim());

                    string boundingBox = GeometryTranslator.Geometry2GML(new Envelope(), String.Empty, gmlVersion);
                    sw.WriteLine(@"
   <gml:boundedBy>");
                    sw.Write(boundingBox);
                    sw.Write(@"
   </gml:boundedBy>");

                    sw.Write(@"
</gml:FeatureCollection>");

                }

                return true;
            }
            catch (Exception ex)
            {
                string errMsg = ex.Message;
                return false;
            }
        }

        private IEnvelope Envelope
        {
            get
            {
                if (_doc == null)
                {
                    return null;
                }

                try
                {
                    XmlNode envNode = _doc.SelectSingleNode("GML:FeatureCollection/GML:boundedBy", _ns);
                    if (envNode == null)
                    {
                        return null;
                    }

                    return GeometryTranslator.GML2Geometry(envNode.InnerXml, _gmlVersion) as IEnvelope;
                }
                catch (Exception ex)
                {
                    _errMsg = ex.Message;
                    return null;
                }
            }
            set
            {
                if (_doc == null || value == null)
                {
                    return;
                }

                try
                {
                    XmlNode coords = _doc.SelectSingleNode("GML:FeatureCollection/GML:boundedBy/GML:Box/GML:coordinates", _ns);
                    if (coords == null)
                    {
                        return;
                    }

                    coords.InnerText =
                        value.MinX.ToString(_nhi) + "," + value.MinY.ToString(_nhi) + " " +
                        value.MaxX.ToString(_nhi) + "," + value.MaxY.ToString(_nhi);

                }
                catch
                {
                }
            }
        }

        private bool UpdateEnvelope(IEnvelope append)
        {
            if (append == null)
            {
                return true;
            }

            IEnvelope envelope = this.Envelope;
            if (envelope == null)
            {
                return false;
            }

            if (envelope.MinX == 0.0 && envelope.MaxX == 0.0 &&
                envelope.MinY == 0.0 && envelope.MaxY == 0.0)
            {
                envelope = append;
            }
            else
            {
                envelope.Union(append);
            }

            this.Envelope = envelope;
            return true;
        }
        public bool AppendFeature(IFeatureClass fc, IFeature feature)
        {
            if (_doc == null || fc == null || feature == null)
            {
                return false;
            }

            XmlNode featureCollection = _doc.SelectSingleNode("GML:FeatureCollection", _ns);
            if (featureCollection == null)
            {
                return false;
            }

            XmlNode featureMember = _doc.CreateElement("gml", "featureMember", _ns.LookupNamespace("GML"));
            XmlNode featureclass = _doc.CreateElement("gv", fc.Name, _ns.LookupNamespace("myns"));

            featureMember.AppendChild(featureclass);

            if (feature.Shape != null)
            {
                try
                {
                    string geom = GeometryTranslator.Geometry2GML(feature.Shape, String.Empty, _gmlVersion);
                    geom = @"<gml:theGeometry xmlns:gml=""http://www.opengis.net/gml"">" + geom + "</gml:theGeometry>";
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(geom);

                    XmlNode geomNode = _doc.CreateElement("gv", fc.ShapeFieldName.Replace("#", ""), _ns.LookupNamespace("myns"));

                    foreach (XmlNode node in doc.ChildNodes[0].ChildNodes)
                    {
                        geomNode.AppendChild(_doc.ImportNode(node, true));
                    }
                    if (!UpdateEnvelope(feature.Shape.Envelope))
                    {
                        _errMsg = "Can't update envelope...";
                        return false;
                    }
                    featureclass.AppendChild(geomNode);
                }
                catch (Exception ex)
                {
                    _errMsg = ex.Message;
                    return false;
                }
            }
            foreach (FieldValue fv in feature.Fields)
            {
                XmlNode attrNode = _doc.CreateElement("gv", fv.Name.Replace("#", ""), _ns.LookupNamespace("myns"));
                if (fv.Value != null)
                {
                    attrNode.InnerText = fv.Value.ToString();
                }

                featureclass.AppendChild(attrNode);
            }
            featureCollection.AppendChild(featureMember);

            return true;
        }

        public bool Flush()
        {
            if (_doc == null)
            {
                return false;
            }

            try
            {
                FileInfo fi = new FileInfo(_filename);
                if (fi.Exists)
                {
                    fi.Delete();
                }

                _doc.Save(_filename);
                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
        }
    }
}
