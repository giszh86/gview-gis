﻿using gView.Framework.Core.Data;
using gView.Framework.Core.FDB;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.IO;
using gView.Framework.Core.Common;
using gView.Framework.Core.UI;
using gView.Framework.Data;
using gView.Framework.Data.Metadata;
using gView.Framework.Geometry;
using gView.Framework.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace gView.DataSources.Fdb.PostgreSql
{
    [RegisterPlugIn("33254063-133D-4b17-AAE2-46AF7A7DA733")]
    public class pgDataset : DatasetMetadata, IFeatureDataset2, IRasterDataset, IFDBDataset, IConnectionStringDialog
    {
        internal int _dsID = -1;
        private List<IDatasetElement> _layers;
        private string _errMsg = String.Empty;
        private string _dsname = String.Empty;
        internal pgFDB _fdb = null;
        private string _connStr = String.Empty;
        private List<string> _addedLayers;
        private ISpatialReference _sRef = null;
        private DatasetState _state = DatasetState.unknown;
        private ISpatialIndexDef _sIndexDef = new gViewSpatialIndexDef();

        public pgDataset()
        {
            _addedLayers = new List<string>();
            _fdb = new pgFDB();
        }


        async static internal Task<pgDataset> Create(pgFDB fdb, string dsname)
        {
            var ds = new pgDataset();

            await ds.SetConnectionString(fdb.DatabaseConnectionString + ";dsname=" + dsname);
            await ds.Open();

            return ds;
        }

        ~pgDataset()
        {
            this.Dispose();
        }
        public void Dispose()
        {
            if (_fdb != null)
            {
                _fdb.Dispose();
                _fdb = null;
            }
        }

        #region IFeatureDataset Member

        async public Task<IEnvelope> Envelope()
        {
            if (_layers == null)
            {
                return null;
            }

            bool first = true;
            IEnvelope env = null;

            foreach (IDatasetElement layer in _layers)
            {
                IEnvelope envelope = null;
                if (layer.Class is IFeatureClass)
                {
                    envelope = ((IFeatureClass)layer.Class).Envelope;
                    if (envelope.Width > 1e10 && await ((IFeatureClass)layer.Class).CountFeatures() == 0)
                    {
                        envelope = null;
                    }
                }
                else if (layer.Class is IRasterClass)
                {
                    if (((IRasterClass)layer.Class).Polygon == null)
                    {
                        continue;
                    }

                    envelope = ((IRasterClass)layer.Class).Polygon.Envelope;
                }
                if (gView.Framework.Geometry.Envelope.IsNull(envelope))
                {
                    continue;
                }

                if (first)
                {
                    first = false;
                    env = new Envelope(envelope);
                }
                else
                {
                    env.Union(envelope);
                }
            }
            return env;
        }

        async public Task<ISpatialReference> GetSpatialReference()
        {

            if (_sRef != null)
            {
                return _sRef;
            }

            if (_fdb == null)
            {
                return null;
            }

            return _sRef = await _fdb.SpatialReference(_dsname);
        }
        public void SetSpatialReference(ISpatialReference sRef)
        {
            // Nicht in Databank übernehmen !!!
            _sRef = sRef;
        }

        #endregion

        #region IDataset Member

        public string DatasetName
        {
            get
            {
                return _dsname;
            }
        }

        public string ConnectionString
        {
            get
            {
                if (_fdb == null)
                {
                    return String.Empty;
                }

                string c = _fdb._conn.ConnectionString + ";" +
                    "dsname=" + ConfigTextStream.ExtractValue(_connStr, "dsname") + ";";

                while (c.IndexOf(";;") != -1)
                {
                    c = c.Replace(";;", ";");
                }

                return c;
            }
        }
        async public Task<bool> SetConnectionString(string value)
        {
            _connStr = value;
            if (value == null)
            {
                return false;
            }

            while (_connStr.IndexOf(";;") != -1)
            {
                _connStr = _connStr.Replace(";;", ";");
            }

            _dsname = ConfigTextStream.ExtractValue(value, "dsname");
            _addedLayers.Clear();
            foreach (string layername in ConfigTextStream.ExtractValue(value, "layers").Split('@'))
            {
                if (layername == "")
                {
                    continue;
                }

                if (_addedLayers.IndexOf(layername) != -1)
                {
                    continue;
                }

                _addedLayers.Add(layername);
            }
            if (_fdb == null)
            {
                _fdb = new pgFDB();
            }

            _fdb.DatasetRenamed += new gView.DataSources.Fdb.MSAccess.AccessFDB.DatasetRenamedEventHandler(SqlFDB_DatasetRenamed);
            _fdb.FeatureClassRenamed += new gView.DataSources.Fdb.MSAccess.AccessFDB.FeatureClassRenamedEventHandler(SqlFDB_FeatureClassRenamed);
            _fdb.TableAltered += new gView.DataSources.Fdb.MSAccess.AccessFDB.AlterTableEventHandler(SqlFDB_TableAltered);
            await _fdb.Open(_connStr);

            return true;
        }


        public DatasetState State
        {
            get { return _state; }
        }

        public string DatasetGroupName
        {
            get
            {
                return "Postgre FDB";
            }
        }

        public string ProviderName
        {
            get
            {
                return "Postgre Feature Database";
            }
        }

        async public Task<bool> Open()
        {
            if (_fdb == null)
            {
                return false;
            }

            _dsID = await _fdb.DatasetID(_dsname);
            if (_dsID < 0)
            {
                return false;
            }

            _sRef = await this.GetSpatialReference();
            _state = DatasetState.opened;
            _sIndexDef = await _fdb.SpatialIndexDef(_dsID);

            return true;
        }

        public string LastErrorMessage
        {
            get
            {
                return _errMsg;
            }
            set { _errMsg = value; }
        }

        async public Task<List<IDatasetElement>> Elements()
        {
            if (_layers == null || _layers.Count == 0)
            {
                _layers = await _fdb.DatasetLayers(this);

                if (_layers != null && _addedLayers.Count != 0)
                {
                    List<IDatasetElement> list = new List<IDatasetElement>();
                    foreach (IDatasetElement element in _layers)
                    {
                        if (element is IFeatureLayer)
                        {
                            if (_addedLayers.Contains(((IFeatureLayer)element).FeatureClass.Name))
                            {
                                list.Add(element);
                            }
                        }
                        else if (element is IRasterLayer)
                        {
                            if (_addedLayers.Contains(((IRasterLayer)element).Title))
                            {
                                list.Add(element);
                            }
                        }
                        else
                        {
                            if (_addedLayers.Contains(element.Title))
                            {
                                list.Add(element);
                            }
                        }
                    }
                    _layers = list;
                }

            }

            if (_layers == null)
            {
                return new List<IDatasetElement>();
            }

            return _layers;
        }

        public string Query_FieldPrefix
        {
            get { return "\""; }
        }

        public string Query_FieldPostfix
        {
            get { return "\""; }
        }

        public IDatabase Database
        {
            get { return _fdb; }
        }

        async public Task<IDatasetElement> Element(string title)
        {
            if (_fdb == null)
            {
                return null;
            }

            IDatasetElement element = await _fdb.DatasetElement(this, title);

            if (element != null && element.Class is pgFeatureClass)
            {
                ((pgFeatureClass)element.Class).SpatialReference = _sRef;
            }

            return element;
        }

        async public Task RefreshClasses()
        {
            if (_fdb != null)
            {
                await _fdb.RefreshClasses(this);
            }
        }

        #endregion

        #region IDataset2 Member

        async public Task<IDataset2> EmptyCopy()
        {
            pgDataset dataset = new pgDataset();
            await dataset.SetConnectionString(ConnectionString);
            await dataset.Open();

            return dataset;
        }

        async public Task AppendElement(string elementName)
        {
            if (_layers == null)
            {
                _layers = new List<IDatasetElement>();
            }

            foreach (IDatasetElement e in _layers)
            {
                if (e.Title == elementName)
                {
                    return;
                }
            }

            IDatasetElement element = await _fdb.DatasetElement(this, elementName);
            if (element != null)
            {
                _layers.Add(element);
            }
        }

        #endregion

        #region IFDBDataset Member

        public ISpatialIndexDef SpatialIndexDef
        {
            get { return _sIndexDef; }
        }

        #endregion

        #region IConnectionStringDialog Member

        public string ShowConnectionStringDialog(string initConnectionString)
        {
            //string appPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            //Assembly uiAssembly = Assembly.LoadFrom(appPath + @"/FDB.dll");

            //gView.Framework.UI.IConnectionStringDialog p = uiAssembly.CreateInstance("gView.FDB.SqlFdbConnectionStringDialog") as gView.Framework.UI.IConnectionStringDialog;
            //if (p != null)
            //    return p.ShowConnectionStringDialog(initConnectionString);

            //return null;
            return null;
        }

        #endregion

        #region IPersistableLoadAsync Member

        public string PersistID
        {
            get
            {
                return null;
            }
        }

        async public Task<bool> LoadAsync(IPersistStream stream)
        {
            string connectionString = (string)stream.Load("connectionstring", "");
            if (_fdb != null)
            {
                _fdb.Dispose();
                _fdb = null;
            }

            await this.SetConnectionString(connectionString);
            return await this.Open();
        }

        public void Save(IPersistStream stream)
        {
            stream.SaveEncrypted("connectionstring", this.ConnectionString);
        }

        #endregion

        void SqlFDB_FeatureClassRenamed(string oldName, string newName)
        {
            if (_layers == null)
            {
                return;
            }

            foreach (IDatasetElement element in _layers)
            {
                if (element.Class is pgFeatureClass &&
                    ((pgFeatureClass)element.Class).Name == oldName)
                {
                    ((pgFeatureClass)element.Class).Name = newName;
                }
            }
        }

        async void SqlFDB_TableAltered(string table)
        {
            if (_layers == null)
            {
                return;
            }

            foreach (IDatasetElement element in _layers)
            {
                if (element.Class is pgFeatureClass &&
                    ((pgFeatureClass)element.Class).Name == table)
                {
                    var fields = await _fdb.FeatureClassFields(this._dsID, table);

                    pgFeatureClass fc = element.Class as pgFeatureClass;
                    ((FieldCollection)fc.Fields).Clear();

                    foreach (IField field in fields)
                    {
                        ((FieldCollection)fc.Fields).Add(field);
                    }
                }
            }
        }

        void SqlFDB_DatasetRenamed(string oldName, string newName)
        {
            if (_dsname == oldName)
            {
                _dsname = newName;
            }
        }
    }
}
