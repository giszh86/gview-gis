using gView.Framework.Core.Data;
using gView.Framework.Core.FDB;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.IO;
using gView.Framework.Core.MapServer;
using gView.Framework.Core.Common;
using gView.Framework.Data;
using gView.Framework.Data.Metadata;
using gView.Framework.Geometry;
using gView.Framework.IO;
using gView.Framework.Web.Abstraction;
using gView.Framework.Web.Services;
using gView.Framework.XML;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace gView.Interoperability.ArcXML.Dataset
{
    [RegisterPlugIn("3B26682C-BF6E-4fe8-BE80-762260ABA581")]
    public class ArcIMSDataset : DatasetMetadata, IFeatureDataset, IRequestDependentDataset
    {
        internal readonly IHttpService _http;

        internal string _connection = "";
        internal string _name = "";
        internal List<IWebServiceTheme> _themes = new List<IWebServiceTheme>();
        private IClass _class = null;
        private IEnvelope _envelope;
        private string _errMsg = "";
        internal ArcXMLProperties _properties = new ArcXMLProperties();
        private DatasetState _state = DatasetState.unknown;
        private ISpatialReference _sRef = null;

        public ArcIMSDataset() 
        {
            _http = HttpService.CreateInstance();
        }

        public ArcIMSDataset(string connection, string name)
            : this()
        {
            _connection = connection;
            _name = name;

            _class = new ArcIMSClass(this);
        }

        internal ArcIMSClass WebServiceClass
        {
            get { return _class as ArcIMSClass; }
        }

        public XmlDocument Properties
        {
            get { return _properties.Properties; }
        }

        #region IFeatureDataset Member

        public Task<IEnvelope> Envelope()
        {
            return Task.FromResult<IEnvelope>(_envelope);
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

        public void Dispose()
        {
            _state = DatasetState.unknown;
        }

        public string ConnectionString
        {
            get
            {
                return _connection + ";service=" + _name;
            }
        }
        public Task<bool> SetConnectionString(string value)
        {
            _connection = "server=" + ConfigTextStream.ExtractValue(value, "server") +
                            ";user=" + ConfigTextStream.ExtractValue(value, "user") +
                            ";pwd=" + ConfigTextStream.ExtractValue(value, "pwd");
            _name = ConfigTextStream.ExtractValue(value, "service");

            return Task.FromResult(true);
        }

        public string DatasetGroupName
        {
            get { return "ESRI ArcIMS"; }
        }

        public string DatasetName
        {
            get { return "ESRI ArcIMS Service"; }
        }

        public string ProviderName
        {
            get { return "ESRI"; }
        }

        public DatasetState State
        {
            get { return _state; }
        }

        async public Task<bool> Open()
        {
            return await Open(null);
        }

        public string LastErrorMessage
        {
            get { return _errMsg; }
            set { _errMsg = value; }
        }

        public int order
        {
            get
            {
                return 0;
            }
            set
            {

            }
        }

        public IDatasetEnum DatasetEnum
        {
            get { return null; }
        }

        public Task<List<IDatasetElement>> Elements()
        {
            List<IDatasetElement> elements = new List<IDatasetElement>();
            if (_class != null)
            {
                elements.Add(new DatasetElement(_class));
            }
            return Task.FromResult<List<IDatasetElement>>(elements);
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
            get { return null; }
        }

        public Task<IDatasetElement> Element(string title)
        {
            if (_class != null && title == _class.Name)
            {
                return Task.FromResult<IDatasetElement>(new DatasetElement(_class));
            }

            return Task.FromResult<IDatasetElement>(null);
        }

        public Task RefreshClasses()
        {
            return Task.CompletedTask;
        }
        #endregion

        #region IRequestDependentDataset Member

        async public Task<bool> Open(IServiceRequestContext context)
        {
            if (_class == null)
            {
                _class = new ArcIMSClass(this);
            }

            string server = ConfigTextStream.ExtractValue(ConnectionString, "server");
            string service = ConfigTextStream.ExtractValue(ConnectionString, "service");
            string user = ConfigTextStream.ExtractValue(ConnectionString, "user");
            string pwd = ConfigTextStream.ExtractValue(ConnectionString, "pwd");

            //if ((user == "#" || user == "$") &&
            //        context != null && context.ServiceRequest != null && context.ServiceRequest.Identity != null)
            //{
            //    string roles = String.Empty;
            //    if (user == "#" && context.ServiceRequest.Identity.UserRoles != null)
            //    {
            //        foreach (string role in context.ServiceRequest.Identity.UserRoles)
            //        {
            //            if (String.IsNullOrEmpty(role)) continue;
            //            roles += "|" + role;
            //        }
            //    }
            //    user = context.ServiceRequest.Identity.UserName + roles;
            //    pwd = context.ServiceRequest.Identity.HashedPassword;
            //}

            dotNETConnector connector = new dotNETConnector();
            if (!String.IsNullOrEmpty(user) || !String.IsNullOrEmpty(pwd))
            {
                connector.setAuthentification(user, pwd);
            }

            try
            {
                _themes.Clear();

                string axl = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ARCXML version=\"1.1\"><REQUEST><GET_SERVICE_INFO fields=\"true\" envelope=\"true\" renderer=\"true\" extensions=\"true\" /></REQUEST></ARCXML>";
                //string axl = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ARCXML version=\"1.1\"><REQUEST><GET_SERVICE_INFO dpi=\"96\" toc=\"true\" /></REQUEST></ARCXML>";

                await ArcIMSClass.LogAsync(context, "GetServiceInfo Response", server, service, axl);
                axl = await connector.SendRequest(axl, server, service);
                await ArcIMSClass.LogAsync(context, "GetServiceInfo Response", server, service, axl);

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(axl);

                double dpi = 96.0;
                XmlNode screen = doc.SelectSingleNode("//ENVIRONMENT/SCREEN");
                if (screen != null)
                {
                    if (screen.Attributes["dpi"] != null)
                    {
                        dpi = Convert.ToDouble(screen.Attributes["dpi"].Value.Replace(".", ","));
                    }
                }
                double dpm = (dpi / 0.0254);

                XmlNode FeatureCoordSysNode = doc.SelectSingleNode("ARCXML/RESPONSE/SERVICEINFO/PROPERTIES/FEATURECOORDSYS");
                _sRef = ArcXMLGeometry.AXL2SpatialReference(FeatureCoordSysNode);

                foreach (XmlNode envelopeNode in doc.SelectNodes("//ENVELOPE"))
                {
                    if (_envelope == null)
                    {
                        _envelope = new Envelope(envelopeNode);
                    }
                    else
                    {
                        _envelope.Union(new Envelope(envelopeNode));
                    }
                }
                foreach (XmlNode layerNode in doc.SelectNodes("//LAYERINFO[@id]"))
                {
                    bool visible = true;

                    if (layerNode.Attributes["visible"] != null)
                    {
                        bool.TryParse(layerNode.Attributes["visible"].Value, out visible);
                    }

                    //XmlNode tocNode = layerNode.SelectSingleNode("TOC");
                    //if (tocNode != null)
                    //{
                    //    ReadTocNode(tocNode);
                    //}

                    IClass themeClass = null;
                    IWebServiceTheme theme;
                    if (layerNode.Attributes["type"] != null && layerNode.Attributes["type"].Value == "featureclass")
                    {
                        themeClass = await ArcIMSThemeFeatureClass.CreateAsync(this, layerNode.Attributes["id"].Value);
                        ((ArcIMSThemeFeatureClass)themeClass).Name = layerNode.Attributes["name"] != null ? layerNode.Attributes["name"].Value : layerNode.Attributes["id"].Value;
                        ((ArcIMSThemeFeatureClass)themeClass).fieldsFromAXL = layerNode.InnerXml;
                        ((ArcIMSThemeFeatureClass)themeClass).SpatialReference = _sRef;

                        XmlNode FCLASS = layerNode.SelectSingleNode("FCLASS[@type]");
                        if (FCLASS != null)
                        {
                            ((ArcIMSThemeFeatureClass)themeClass).fClassTypeString = FCLASS.Attributes["type"].Value;
                        }
                        foreach (XmlNode child in layerNode.ChildNodes)
                        {
                            switch (child.Name)
                            {
                                case "SIMPLERENDERER":
                                case "SIMPLELABELRENDERER":
                                case "VALUEMAPRENDERER":
                                case "SCALEDEPENDENTRENDERER":
                                case "VALUEMAPLABELRENDERER":
                                case "GROUPRENDERER":
                                    ((ArcIMSThemeFeatureClass)themeClass).OriginalRendererNode = child;
                                    break;
                            }
                        }
                        theme = LayerFactory.Create(themeClass, _class as IWebServiceClass) as IWebServiceTheme;
                        if (theme == null)
                        {
                            continue;
                        }

                        theme.Visible = visible;
                    }
                    else if (layerNode.Attributes["type"] != null && layerNode.Attributes["type"].Value == "image")
                    {
                        //themeClass = new ArcIMSThemeRasterClass(this,
                        //    layerNode.Attributes["name"] != null ? layerNode.Attributes["name"].Value : layerNode.Attributes["id"].Value);
                        themeClass = new ArcIMSThemeRasterClass(this, layerNode.Attributes["id"].Value);
                        ((ArcIMSThemeRasterClass)themeClass).Name = layerNode.Attributes["name"] != null ? layerNode.Attributes["name"].Value : layerNode.Attributes["id"].Value;

                        theme = new WebServiceTheme(
                            themeClass,
                            themeClass.Name,
                            layerNode.Attributes["id"].Value,
                            visible,
                            _class as IWebServiceClass);
                    }
                    else
                    {
                        continue;
                    }

                    try
                    {
                        if (layerNode.Attributes["minscale"] != null)
                        {
                            theme.MinimumScale = Convert.ToDouble(layerNode.Attributes["minscale"].Value.Replace(".", ",")) * dpm;
                        }

                        if (layerNode.Attributes["maxscale"] != null)
                        {
                            theme.MaximumScale = Convert.ToDouble(layerNode.Attributes["maxscale"].Value.Replace(".", ",")) * dpm;
                        }
                    }
                    catch { }
                    _themes.Add(theme);
                }
                _state = DatasetState.opened;

                ((ArcIMSClass)_class).SpatialReference = await this.GetSpatialReference();

                return true;
            }
            catch (Exception ex)
            {
                _state = DatasetState.unknown;
                _errMsg = ex.Message;
                await ArcIMSClass.ErrorLog(context, "Open Dataset", server, service, ex);
                return false;
            }
        }

        #endregion

        #region IPersistableAsync Member

        async public Task<bool> LoadAsync(IPersistStream stream)
        {
            await this.SetConnectionString((string)stream.Load("ConnectionString", ""));
            _properties = stream.Load("Properties", new ArcXMLProperties(), new ArcXMLProperties()) as ArcXMLProperties;

            _class = new ArcIMSClass(this);
            return await Open();
        }

        public void Save(IPersistStream stream)
        {
            stream.Save("ConnectionString", this.ConnectionString);
            stream.Save("Properties", _properties);
        }

        #endregion

        #region Helper

        //private void ReadTocNode(XmlNode tocNode)
        //{
        //    foreach (XmlNode tocclass in tocNode.SelectNodes("TOCGROUP/TOCCLASS"))
        //    {
        //        try
        //        {
        //            MemoryStream ms = new MemoryStream();
        //            byte[] imgBytes = Convert.FromBase64String(tocclass.InnerText);
        //            ms.Write(imgBytes, 0, imgBytes.Length);
        //            ms.Position = 0;

        //            System.Drawing.Image image = System.Drawing.Image.FromStream(ms);
        //            image.Dispose();
        //        }
        //        catch { }
        //    }
        //}

        #endregion
    }
}
