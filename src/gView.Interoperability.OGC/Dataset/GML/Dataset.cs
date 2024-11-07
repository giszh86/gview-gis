using gView.Framework.Core.Data;
using gView.Framework.Core.FDB;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.IO;
using gView.Framework.Core.Common;
using gView.Framework.Data;
using gView.Framework.Data.Metadata;
using gView.Framework.OGC.GML;
using gView.Framework.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace gView.Interoperability.OGC.Dataset.GML
{
    [RegisterPlugIn("DBABE7F1-FE46-4731-AB2B-8A324C60554E")]
    public class Dataset : DatasetMetadata, IFeatureDataset
    {
        private string _connectionString;
        private DatasetState _state = DatasetState.unknown;
        private IEnvelope _envelope = null;
        private ISpatialReference _sRef = null;
        private string _errMsg = "";
        private XmlDocument _doc = null;
        private XmlNamespaceManager _ns = null;
        private XmlNode _featureCollection = null;
        private List<IDatasetElement> _elements = new List<IDatasetElement>();
        private string _gml_file = "", _xsd_file = "";
        private Database _database = new Database();
        private GmlVersion _gmlVersion = GmlVersion.v1;

        public XmlNamespaceManager NamespaceManager
        {
            get { return _ns; }
        }

        public XmlNode FeatureCollection
        {
            get { return _featureCollection; }
        }
        public string GmlFileName
        {
            get { return _gml_file; }
        }

        public bool Delete()
        {
            try
            {
                if (_state != DatasetState.opened)
                {
                    return false;
                }

                FileInfo fi = new FileInfo(_gml_file);
                if (fi.Exists)
                {
                    fi.Delete();
                }

                fi = new FileInfo(_xsd_file);
                if (fi.Exists)
                {
                    fi.Delete();
                }

                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
        }

        async internal Task<GMLFile> GetGMLFile()
        {
            try
            {
                if (_state != DatasetState.opened)
                {
                    return null;
                }

                FileInfo fi = new FileInfo(_connectionString);
                if (fi.Exists)
                {
                    return await GMLFile.Create(_connectionString);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        internal string targetNamespace
        {
            get
            {
                if (_ns == null)
                {
                    return String.Empty;
                }

                return _ns.LookupNamespace("myns");
            }
        }

        #region IFeatureDataset Member

        public Task<IEnvelope> Envelope()
        {
            return Task.FromResult(_envelope);
        }

        public Task<ISpatialReference> GetSpatialReference()
        {
            return Task.FromResult(_sRef);
        }
        public void SetSpatialReference(ISpatialReference sRef)
        {
            _sRef = sRef;
        }

        #endregion

        #region IDataset Member

        public string ConnectionString
        {
            get
            {
                return _connectionString;
            }
        }
        public Task<bool> SetConnectionString(string value)
        {
            _connectionString = value;
            _database.DirectoryName = _connectionString;

            return Task.FromResult(true);
        }


        public string DatasetGroupName
        {
            get { return "OGC/GML Dataset"; }
        }

        public string DatasetName
        {
            get { return "GML Dataset"; }
        }

        public string ProviderName
        {
            get { return "gView GML Provider"; }
        }

        public DatasetState State
        {
            get { return _state; }
        }

        async public Task<bool> Open()
        {
            try
            {
                _state = DatasetState.unknown;
                _elements.Clear();

                FileInfo fi_gml = new FileInfo(_connectionString);
                if (!fi_gml.Exists)
                {
                    return true;
                }

                FileInfo fi_xsd = new FileInfo(fi_gml.FullName.Substring(0, fi_gml.FullName.Length - fi_gml.Extension.Length) + ".xsd");
                if (!fi_xsd.Exists)
                {
                    return false;
                }

                _gml_file = fi_gml.FullName;
                _xsd_file = fi_xsd.FullName;

                XmlDocument schema = new XmlDocument();
                schema.LoadXml(System.IO.File.ReadAllText(fi_xsd.FullName));
                XmlSchemaReader schemaReader = new XmlSchemaReader(schema);
                string targetNamespace = schemaReader.TargetNamespaceURI;
                if (targetNamespace == String.Empty)
                {
                    return false;
                }

                PlugInManager compMan = new PlugInManager();
                foreach (string elementName in schemaReader.ElementNames)
                {
                    string shapeFieldName;
                    GeometryType geomType;
                    FieldCollection fields = schemaReader.ElementFields(elementName, out shapeFieldName, out geomType);
                    FeatureClass fc = await FeatureClass.CreateAsync(this, elementName, fields);
                    fc.ShapeFieldName = shapeFieldName;
                    fc.GeometryType = geomType;
                    IFeatureLayer layer = LayerFactory.Create(fc) as IFeatureLayer;
                    if (layer == null)
                    {
                        continue;
                    }

                    //layer.FeatureRenderer = compMan.getComponent(KnownObjects.Carto_UniversalGeometryRenderer) as IFeatureRenderer;

                    _elements.Add(layer);
                }

                _doc = new XmlDocument();
                using (XmlTextReader xmlTextReader = new XmlTextReader(fi_gml.FullName))
                {
                    xmlTextReader.ReadToDescendant("boundedBy", "http://www.opengis.net/gml");
                    string boundedBy = xmlTextReader.ReadOuterXml();

                    _doc.LoadXml(boundedBy);
                }
                _ns = new XmlNamespaceManager(_doc.NameTable);
                _ns.AddNamespace("GML", "http://www.opengis.net/gml");
                _ns.AddNamespace("WFS", "http://www.opengis.net/wfs");
                _ns.AddNamespace("OGC", "http://www.opengis.net/ogc");
                _ns.AddNamespace("myns", targetNamespace);
                XmlNode boundedByNode = _doc.ChildNodes[0];

                if (boundedByNode != null)
                {
                    XmlNode geomNode = boundedByNode.SelectSingleNode("GML:*", _ns);
                    if (geomNode != null)
                    {
                        _envelope = GeometryTranslator.GML2Geometry(geomNode.OuterXml, _gmlVersion) as IEnvelope;
                        if (geomNode.Attributes["srsName"] != null)
                        {
                            _sRef = gView.Framework.Geometry.SpatialReference.FromID(geomNode.Attributes["srsName"].Value);
                        }
                    }
                }

                _state = DatasetState.opened;
                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
        }

        public string LastErrorMessage
        {
            get { return _errMsg; }
            set { _errMsg = value; }
        }

        public Task<List<IDatasetElement>> Elements()
        {
            return Task.FromResult(ListOperations<IDatasetElement>.Clone(_elements));
        }

        public string Query_FieldPrefix
        {
            get { return ""; }
        }

        public string Query_FieldPostfix
        {
            get { return ""; }
        }

        public IDatabase Database
        {
            get { return _database; }
        }

        async public Task<IDatasetElement> Element(string title)
        {
            foreach (IDatasetElement element in _elements)
            {
                if (element.Title == title)
                {
                    return element;
                }
            }

            try
            {
                DirectoryInfo di = new DirectoryInfo(_connectionString);
                if (!di.Exists)
                {
                    return null;
                }

                Dataset ds = new Dataset();
                await ds.SetConnectionString(di + @"/" + title + ".gml");
                if (await ds.Open())
                {
                    return await ds.Element(title);
                }
            }
            catch { }

            return null;
        }

        public Task RefreshClasses()
        {
            return Task.CompletedTask;
        }
        #endregion

        #region IDisposable Member

        public void Dispose()
        {

        }

        #endregion

        #region IPersistableLoadAsync

        public Task<bool> LoadAsync(IPersistStream stream)
        {
            return Task.FromResult(true);
        }

        public void Save(IPersistStream stream)
        {
        }

        #endregion
    }
}
