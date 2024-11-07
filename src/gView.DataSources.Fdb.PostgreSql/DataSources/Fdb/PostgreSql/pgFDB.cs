﻿using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Cursors;
using gView.Framework.Core.Data.Filters;
using gView.Framework.Core.FDB;
using gView.Framework.Core.Geometry;
using gView.Framework.Core.Common;
using gView.Framework.Data;
using gView.Framework.Data.Filters;
using gView.Framework.Db;
using gView.Framework.Db.Extensions;
using gView.Framework.Geometry;
using gView.Framework.Offline;
using gView.Framework.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gView.DataSources.Fdb.PostgreSql
{
    [RegisterPlugIn("a408f01d-7237-4e33-81ba-ac9d29dfc433")]
    public class pgFDB : gView.DataSources.Fdb.MSAccess.AccessFDB
    {
        internal static DbProviderFactory _dbProviderFactory = DataProvider.PostgresProvider;

        public pgFDB()
            : base()
        {
        }

        #region IFDB

        override public void Dispose()
        {
            if (_conn != null)
            {
                _conn.Dispose();
            }

            _conn = null;

            base.Dispose();
        }

        override public bool Create(string name)
        {
            return Create(name, new UserData());
        }

        public bool Create(string name, UserData parameters)
        {
            if (_conn == null)
            {
                _errMsg = "(Create) - No Connection: Use Open() before you call this methode...";
                return false;
            }

            StreamReader reader = new StreamReader(SystemVariables.StartupDirectory + @"/sql/postgreFDB/createdatabase.sql");
            string line = String.Empty;
            StringBuilder sql = new StringBuilder();
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Trim().Contains(";"))
                {
                    sql.Append(line);

                    bool execute = true;

                    if (sql.ToString().ToLower().IndexOf("create database") == 0 && false.Equals(parameters.GetUserData("CreateDatabase")))
                    {
                        execute = false;
                    }

                    if (execute)
                    {
                        if (!_conn.ExecuteNoneQuery(sql.ToString().Replace("#fdb#", name).Replace("\t", " ").Replace(";", String.Empty)))
                        {
                            _errMsg = _conn.errorMessage;
                            _conn.Dispose();
                            reader.Close();
                            return false;
                        }
                    }
                    if (sql.ToString().ToLower().IndexOf("create database") == 0)
                    {
                        _conn.ConnectionString += ";database=" + name;
                    }

                    sql = new StringBuilder();
                }
                else
                {
                    sql.Append(line);
                }
            }
            reader.Close();

            return true;
        }

        async public override Task<IFeatureDataset> GetDataset(string dsname)
        {
            pgDataset dataset = await pgDataset.Create(this, dsname);
            if (dataset._dsID == -1)
            {
                _errMsg = "Dataset '" + dsname + "' does not exist!";
                return null;
            }
            return dataset;
        }

        async public override Task<bool> Open(string connString)
        {
            try
            {
                connString = DbConnectionString.ParseNpgsqlConnectionString(connString);

                if (_conn != null)
                {
                    _conn.Dispose();
                }

                _conn = new CommonDbConnection(connString);

                if (connString.ToLower().StartsWith("npgsql:"))
                {
                    ((CommonDbConnection)_conn).ConnectionString2 = parseConnectionString(connString);
                }
                else
                {
                    _conn.ConnectionString = parseConnectionString(connString);
                    _conn.dbType = gView.Framework.Db.DBType.npgsql;
                }

                await SetVersion();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override string ConnectionString
        {
            get
            {
                if (_conn == null)
                {
                    return "";
                }

                return _conn.ConnectionString;
            }
        }
        #endregion

        #region private Members
        private string parseConnectionString(string connString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string p in connString.Split(';'))
            {
                if (p.ToLower().IndexOf("dsname=") == 0)
                {
                    _dsname = gView.Framework.IO.ConfigTextStream.ExtractValue(connString, "dsname");
                    continue;
                }
                if (p.ToLower().IndexOf("layers=") == 0 || p.ToLower().IndexOf("layer=") == 0)
                {
                    continue;
                }
                if (sb.Length > 0)
                {
                    sb.Append(";");
                }

                sb.Append(p);
            }
            return sb.ToString();
        }
        #endregion

        #region Internals
        async override public Task<IDatasetElement> DatasetElement(IDataset dataset, string elementName)
        {
            pgDataset pgDataset = dataset as pgDataset;
            if (pgDataset == null)
            {
                throw new Exception("datasset is null or not an PostgresDataset");
            }

            ISpatialReference sRef = await this.SpatialReference(pgDataset.DatasetName);

            if (pgDataset.DatasetName == elementName)
            {
                var isImageDatasetResult = await IsImageDataset(pgDataset.DatasetName);
                string imageSpace = isImageDatasetResult.imageSpace;
                if (isImageDatasetResult.isImageDataset)
                {
                    IDatasetElement fLayer = (await DatasetElement(pgDataset, elementName + "_IMAGE_POLYGONS")) as IDatasetElement;
                    if (fLayer != null && fLayer.Class is IFeatureClass)
                    {
                        pgImageCatalogClass iClass = new pgImageCatalogClass(pgDataset, this, fLayer.Class as IFeatureClass, sRef, imageSpace);
                        iClass.SpatialReference = sRef;
                        return new DatasetElement(iClass);
                    }
                }
            }

            DataTable tab = await _conn.Select("*", TableName("FDB_FeatureClasses"), DbColName("DatasetID") + "=" + pgDataset._dsID + " AND " + DbColName("Name") + "='" + elementName + "'");
            if (tab == null || tab.Rows == null)
            {
                _errMsg = _conn.errorMessage;
                return null;
            }
            else if (tab.Rows.Count == 0)
            {
                _errMsg = "Can't find dataset element '" + elementName + "'...";
                return null;
            }

            //if (_seVersion != 0)
            //{
            //    _conn.ExecuteNoneQuery("execute LoadIndex '" + elementName + "'");
            //}
            DataRow row = tab.Rows[0];
            DataRow fcRow = row;

            if (IsLinkedFeatureClass(row))
            {
                IDataset linkedDs = await LinkedDataset(LinkedDatasetCacheInstance, LinkedDatasetId(row));
                if (linkedDs == null)
                {
                    return null;
                }

                IDatasetElement linkedElement = await linkedDs.Element((string)row["Name"]);

                LinkedFeatureClass fc = new LinkedFeatureClass(pgDataset,
                    linkedElement != null && linkedElement.Class is IFeatureClass ? linkedElement.Class as IFeatureClass : null,
                    (string)row["Name"]);

                pgDatasetElement linkedLayer = new pgDatasetElement(fc);
                return linkedLayer;
            }

            if (row["Name"].ToString().Contains("@"))  // Geographic View
            {
                string[] viewNames = row["Name"].ToString().Split('@');
                if (viewNames.Length != 2)
                {
                    return null;
                }

                DataTable tab2 = await _conn.Select("*", TableName("FDB_FeatureClasses"), DbColName("DatasetID") + "=" + pgDataset._dsID + " AND " + DbColName("Name") + "='" + viewNames[0] + "'");
                if (tab2 == null || tab2.Rows.Count != 1)
                {
                    return null;
                }

                fcRow = tab2.Rows[0];
            }

            GeometryDef geomDef;
            if (fcRow.Table.Columns["hasz"] != null)
            {
                geomDef = new GeometryDef((GeometryType)fcRow["geometrytype"], null, (bool)fcRow["hasz"]);
            }
            else
            {  // alte Version war immer 3D
                geomDef = new GeometryDef((GeometryType)fcRow["geometrytype"], null, true);
            }

            DatasetElement layer = await pgDatasetElement.Create(this, pgDataset, row["name"].ToString(), geomDef);
            if (layer.Class is pgFeatureClass) // kann auch SqlFDBNetworkClass sein
            {
                ((pgFeatureClass)layer.Class).Envelope = await this.FeatureClassExtent(layer.Class.Name);
                ((pgFeatureClass)layer.Class).IDFieldName = "FDB_OID";
                ((pgFeatureClass)layer.Class).ShapeFieldName = "FDB_SHAPE";
                //((SqlFDBFeatureClass)layer.FeatureClass).SetSpatialTreeInfo(this.SpatialTreeInfo(row["Name"].ToString()));
                ((pgFeatureClass)layer.Class).SpatialReference = sRef;
            }
            var fields = await this.FeatureClassFields(pgDataset._dsID, layer.Class.Name);
            if (fields != null && layer.Class is ITableClass)
            {
                foreach (IField field in fields)
                {
                    ((FieldCollection)((ITableClass)layer.Class).Fields).Add(field);
                }
            }

            return layer;
        }

        internal string GetFeatureClassDbSchema(string fcName)
        {
            return GetTableDbSchema("FC_" + fcName);
        }

        internal string GetTableDbSchema(string tableName)
        {
            try
            {
                using (DbConnection conn = _dbProviderFactory.CreateConnection())
                {
                    conn.ConnectionString = _conn.ConnectionString;
                    DbCommand cmd = _dbProviderFactory.CreateCommand();
                    cmd.CommandText = "SELECT n.nspname as \"Schema\" FROM pg_catalog.pg_class c LEFT JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace WHERE c.relname='" + tableName + "'";
                    cmd.Connection = conn;

                    conn.Open();
                    object obj = cmd.ExecuteScalar();
                    if (obj != null)
                    {
                        string schema = obj.ToString();
                        return (schema != schema.ToLower()) ? "\"" + schema + "\"" : schema;
                    }
                    return String.Empty;
                }
            }
            catch { return String.Empty; }
        }

        async internal Task<DataTable> Select(string fields, string from, string where)
        {
            if (_conn == null)
            {
                return null;
            }

            return await _conn.Select(fields, from, where);
        }
        #endregion

        #region Overrides
        protected override bool CreateTable(string name, IFieldCollection Fields, bool msSpatial)
        {
            try
            {
                StringBuilder fields = new StringBuilder();
                StringBuilder types = new StringBuilder();

                bool first = true;
                bool hasID = false;

                string idField = String.Empty;

                foreach (IField field in Fields.ToEnumerable())
                {
                    //if( field.type==FieldType.ID ||
                    //if(	field.type==FieldType.Shape ) continue;

                    if (!first)
                    {
                        fields.Append(";");
                        types.Append(";");
                    }
                    first = false;

                    string fname = field.name.Replace("#", "_");

                    fields.Append(fname);

                    switch (field.type)
                    {
                        case FieldType.biginteger:
                            types.Append("bigint NULL");
                            break;
                        case FieldType.integer:
                        case FieldType.smallinteger:
                            types.Append("int NULL");
                            break;
                        case FieldType.boolean:
                            types.Append("boolean NULL");
                            break;
                        case FieldType.Float:
                            types.Append("float4 NULL");
                            break;
                        case FieldType.Double:
                            types.Append("float8 NULL");
                            break;
                        case FieldType.Date:
                            types.Append("timestamp NULL");
                            break;
                        case FieldType.ID:
                            if (!hasID)
                            {
                                hasID = true;
                                idField = field.name;
                                types.Append("serial primary key");
                            }
                            else
                            {
                                types.Append("int NULL");
                            }
                            break;
                        case FieldType.Shape:
                        case FieldType.binary:
                            types.Append("bytea NULL");
                            break;
                        case FieldType.character:
                            types.Append("varchar(1) NULL");
                            break;
                        case FieldType.String:
                        case FieldType.NString:
                            if (field.size > 0)
                            {
                                types.Append("varchar(" + Math.Min(field.size, 4000) + ")");
                            }
                            else if (field.size <= 0)
                            {
                                types.Append("varchar(255) NULL");
                            }

                            break;
                        case FieldType.guid:
                            //types.Append("varchar(40) NULL");
                            types.Append("uuid NULL");
                            break;
                        case FieldType.replicationID:
                            //types.Append("varchar(40) NULL");
                            //types.Append("[uniqueidentifier] ROWGUIDCOL DEFAULT (newid())");
                            types.Append("uuid NULL");
                            break;
                        default:
                            types.Append("varchar(255) NULL");
                            break;

                    }
                }

                if (!_conn.createTable(name, fields.ToString().Split(';'), types.ToString().Split(';')))
                {
                    _errMsg = _conn.errorMessage;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
        }

        async protected override Task<bool> TableExists(string tableName)
        {
            if (_conn == null)
            {
                return false;
            }

            try
            {
                if (tableName.Contains("."))  // Ohne Schema abfragen...
                {
                    tableName = tableName.Split('.')[1];
                }

                /* string sql =
@"SELECT count(a.attname) as ""colcount"" 
FROM pg_catalog.pg_attribute a
WHERE a.attnum > 0
AND NOT a.attisdropped
AND a.attrelid = (
SELECT c.oid
FROM pg_catalog.pg_class c
LEFT JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname = '" + tableName.Replace("\"", "") + @"'
AND pg_catalog.pg_table_is_visible(c.oid)
)"; */

                string sql =
@"SELECT c.oid as ""colcount""
FROM pg_catalog.pg_class c
WHERE c.relname = '" + tableName.Replace("\"", "") + @"'";

                int exists = Convert.ToInt32(await _conn.QuerySingleField(sql, "colcount"));

                return exists > 0;
            }
            catch
            {
                return false;
            }
        }

        async public override Task<List<string>> DatabaseTables()
        {
            if (_conn == null)
            {
                return new List<string>();
            }

            try
            {
                DataTable tab = await _conn.Select("relname", "pg_catalog.pg_class", "relkind='r'");
                if (tab != null)
                {
                    List<string> tables = new List<string>();
                    foreach (DataRow row in tab.Rows)
                    {
                        if (row["relname"].ToString().StartsWith("pg_"))
                        {
                            continue;
                        }

                        tables.Add(row["relname"].ToString());
                    }
                    return tables;
                }
            }
            catch
            {
            }
            return new List<string>();
        }

        #region System Views List
        private List<string> _systemViews = new List<string>(new string[] { "pg_roles", "pg_shadow", "pg_group", "pg_user", "pg_rules", "pg_views", "pg_tables", "pg_indexes", "pg_stats", "pg_locks", "pg_cursors", "pg_available_extensions", "pg_available_extension_versions", "pg_prepared_xacts", "pg_prepared_statements", "pg_settings", "pg_timezone_abbrevs", "pg_timezone_names", "pg_stat_all_tables", "pg_stat_xact_all_tables", "pg_stat_sys_tables", "pg_stat_xact_sys_tables", "pg_stat_user_tables", "pg_stat_xact_user_tables", "pg_statio_all_tables", "pg_statio_sys_tables", "pg_statio_user_tables", "pg_stat_all_indexes", "pg_stat_sys_indexes", "pg_stat_user_indexes", "pg_statio_all_indexes", "pg_statio_sys_indexes", "pg_statio_user_indexes", "pg_statio_all_sequences", "pg_statio_sys_sequences", "pg_statio_user_sequences", "pg_stat_database_conflicts", "pg_stat_user_functions", "pg_stat_xact_user_functions", "pg_stat_bgwriter", "pg_user_mappings", "pg_seclabels", "pg_stat_activity", "pg_stat_replication", "pg_stat_database", "information_schema_catalog_name", "applicable_roles", "administrable_role_authorizations", "attributes", "character_sets", "check_constraint_routine_usage", "check_constraints", "collations", "collation_character_set_applicability", "column_domain_usage", "column_privileges", "column_udt_usage", "columns", "constraint_column_usage", "constraint_table_usage", "domain_constraints", "domain_udt_usage", "domains", "enabled_roles", "key_column_usage", "parameters", "referential_constraints", "role_column_grants", "routine_privileges", "role_routine_grants", "routines", "schemata", "sequences", "table_constraints", "table_privileges", "role_table_grants", "tables", "triggered_update_columns", "triggers", "usage_privileges", "role_usage_grants", "view_column_usage", "view_routine_usage", "view_table_usage", "views", "data_type_privileges", "element_types", "_pg_foreign_data_wrappers", "foreign_data_wrapper_options", "foreign_data_wrappers", "_pg_foreign_servers", "foreign_server_options", "foreign_servers", "_pg_foreign_tables", "foreign_table_options", "foreign_tables", "_pg_user_mappings", "user_mapping_options", "user_mappings" });
        #endregion

        async public override Task<List<string>> DatabaseViews()
        {
            if (_conn == null)
            {
                return new List<string>();
            }

            try
            {
                DataTable tab = await _conn.Select("relname", "pg_catalog.pg_class", "relkind='v'");
                if (tab != null)
                {
                    List<string> views = new List<string>();
                    foreach (DataRow row in tab.Rows)
                    {
                        if (_systemViews.Contains(row["relname"].ToString()))
                        {
                            continue;
                        }

                        views.Add(row["relname"].ToString());
                    }

                    return views;
                }
            }
            catch
            {
            }
            return new List<string>();
        }

        async protected Task<bool> CheckTablePrivilege(string tableName)
        {
            if (_conn == null)
            {
                return false;
            }

            try
            {
                string sql = "SELECT has_table_privilege('" + tableName + "', 'select') as p";
                return Convert.ToBoolean(await _conn.QuerySingleField(sql, "p"));
            }
            catch
            {
                return false;
            }
        }

        async public override Task<IFeatureClass> GetFeatureclass(string dsName, string fcName)
        {
            pgDataset dataset = new pgDataset();
            await dataset.SetConnectionString(_conn.ConnectionString + ";dsname=" + dsName);
            await dataset.Open();

            IDatasetElement element = await dataset.Element(fcName);
            if (element != null && element.Class is IFeatureClass)
            {
                return element.Class as IFeatureClass;
            }
            else
            {
                dataset.Dispose();
                return null;
            }
        }

        protected override string FieldDataType(IField field)
        {
            switch (field.type)
            {
                case FieldType.biginteger:
                    return "int8 NULL";
                case FieldType.integer:
                case FieldType.smallinteger:
                    return "int4 NULL";
                case FieldType.boolean:
                    return "boolean NULL";
                case FieldType.Float:
                    return "float4 NULL";
                case FieldType.Double:
                    return "float8 NULL";
                case FieldType.Date:
                    return "datetime NULL";
                case FieldType.ID:
                    return "int";
                case FieldType.Shape:
                case FieldType.binary:
                    return "bytea NULL";
                case FieldType.character:
                    return "varchar(1) NULL";
                case FieldType.String:
                    if (field.size > 0)
                    {
                        return "varchar(" + Math.Min(field.size, 4000) + ")";
                    }
                    else if (field.size <= 0)
                    {
                        return "varchar(255) NULL";
                    }

                    break;
                case FieldType.guid:
                    //return "varchar(40) NULL";
                    return "uuid NULL";
                case FieldType.replicationID:
                    //return "varchar(40) NULL";
                    return "uuid NULL";
                //return "[uniqueidentifier] ROWGUIDCOL DEFAULT (newid())";
                default:
                    return "varchar(255) NULL";
            }
            return String.Empty;
        }

        async override public Task<IFeatureCursor> Query(IFeatureClass fc, IQueryFilter filter)
        {
            if (_conn == null || fc == null || !(fc.Dataset is IFDBDataset))
            {
                return null;
            }

            filter.fieldPostfix = filter.fieldPrefix = "\"";
            if (filter is IBufferQueryFilter)
            {
                ISpatialFilter bFilter = await BufferQueryFilter.ConvertToSpatialFilter(filter as IBufferQueryFilter);
                if (bFilter == null)
                {
                    return null;
                }

                return await Query(fc, bFilter);
            }
            if (filter is ISpatialFilter)
            {
                filter = SpatialFilter.Project(filter as ISpatialFilter, fc.SpatialReference);
            }

            string subfields = String.Empty;
            if (filter != null)
            {
                if (filter is ISpatialFilter)
                {
                    if (((ISpatialFilter)filter).SpatialRelation != spatialRelation.SpatialRelationEnvelopeIntersects)
                    {
                        filter.AddField("FDB_SHAPE");
                    }
                }
                //subfields=filter.SubFields.Replace(" ",",");
                subfields = filter.SubFieldsAndAlias;
            }
            if (subfields == String.Empty)
            {
                subfields = "*";
            }

            List<long> NIDs = null;
            ISpatialFilter sFilter = null;

            if (filter is ISpatialFilter)
            {
                sFilter = (ISpatialFilter)filter;

                await CheckSpatialSearchTreeVersion(fc.Name);
                if (_spatialSearchTrees[OriginFcName(fc.Name)] == null)
                {
                    _spatialSearchTrees[OriginFcName(fc.Name)] = await this.SpatialSearchTree(fc.Name);
                }
                ISearchTree tree = (ISearchTree)_spatialSearchTrees[OriginFcName(fc.Name)];
                if (tree != null && ((ISpatialFilter)filter).Geometry != null)
                {
                    if (sFilter.SpatialRelation == spatialRelation.SpatialRelationMapEnvelopeIntersects &&
                        sFilter.Geometry is IEnvelope)
                    {
                        NIDs = tree.CollectNIDsPlus((IEnvelope)sFilter.Geometry);
                    }
                    else
                    {
                        NIDs = tree.CollectNIDs(sFilter.Geometry);
                    }
                }
                if (((ISpatialFilter)filter).SpatialRelation == spatialRelation.SpatialRelationMapEnvelopeIntersects)
                {
                    sFilter = null;
                }
            }

            string tabName = ((fc is pgFeatureClass) ? ((pgFeatureClass)fc).DbTableName : "FC_" + fc.Name);

            string sql = "SELECT " + subfields + " FROM " + tabName;
            string where = String.Empty, orederBy = String.Empty;
            if (filter != null)
            {
                if (!String.IsNullOrEmpty(filter.WhereClause))
                {
                    where = filter.WhereClause;
                }
                if (!String.IsNullOrEmpty(filter.OrderBy))
                {
                    orederBy = filter.OrderBy;
                }
            }

            return await pgFeatureCursor.Create(_conn.ConnectionString, sql, DataProvider.ToDbWhereClause("npgsql", where), orederBy, filter.Limit, filter.BeginRecord, filter.NoLock, NIDs, sFilter, fc,
                (filter != null) ? filter.FeatureSpatialReference : null);

        }

        async override public Task<IFeatureCursor> QueryIDs(IFeatureClass fc, string subFields, List<int> IDs, ISpatialReference toSRef)
        {
            string tabName = ((fc is pgFeatureClass) ? ((pgFeatureClass)fc).DbTableName : "fc_" + fc.Name);
            string sql = "SELECT " + subFields + " FROM " + tabName;
            return await pgFeatureCursorIDs.Create(_conn.ConnectionString, sql, IDs, await this.GetGeometryDef(fc.Name), toSRef);
        }

        async override public Task<List<IDatasetElement>> DatasetLayers(IDataset dataset)
        {
            _errMsg = String.Empty;
            if (_conn == null)
            {
                return null;
            }

            int dsID = await this.DatasetID(dataset.DatasetName);
            if (dsID == -1)
            {
                return null;
            }

            DataSet ds = new DataSet();
            if (!await _conn.SQLQuery(ds, "SELECT * FROM " + TableName("FDB_FeatureClasses") + " WHERE " + DbColName("DatasetID") + "=" + dsID, "FC"))
            {
                _errMsg = _conn.errorMessage;
                return null;
            }

            List<IDatasetElement> layers = new List<IDatasetElement>();
            ISpatialReference sRef = await SpatialReference(dataset.DatasetName);

            var isDatasetImageResult = await IsImageDataset(dataset.DatasetName);
            string imageSpace = isDatasetImageResult.imageSpace;
            if (isDatasetImageResult.isImageDataset)
            {
                if (await TableExists("FC_" + dataset.DatasetName + "_IMAGE_POLYGONS") &&
                    await CheckTablePrivilege(FcTableName(dataset.DatasetName + "_IMAGE_POLYGONS")))
                {
                    IFeatureClass fc = await pgFeatureClass.Create(this, dataset, new GeometryDef(GeometryType.Polygon, sRef, false));
                    ((pgFeatureClass)fc).Name = dataset.DatasetName + "_IMAGE_POLYGONS";
                    ((pgFeatureClass)fc).Envelope = await this.FeatureClassExtent(fc.Name);
                    ((pgFeatureClass)fc).IDFieldName = "FDB_OID";
                    ((pgFeatureClass)fc).ShapeFieldName = "FDB_SHAPE";

                    var fields = await this.FeatureClassFields(dataset.DatasetName, fc.Name);
                    if (fields != null)
                    {
                        foreach (IField field in fields)
                        {
                            ((FieldCollection)fc.Fields).Add(field);
                        }
                    }

                    IClass iClass = null;
                    if (imageSpace.ToLower() == "dataset") // gibts eigentlich noch gar nicht...
                    {
                        throw new NotImplementedException("Rasterdatasets are not implemented in this software version!");
                        //iClass = new SqlFDBImageDatasetClass(dataset as IRasterDataset, this, fc, sRef, imageSpace);
                        //((SqlFDBImageDatasetClass)iClass).SpatialReference = sRef;
                    }
                    else  // RasterCatalog
                    {
                        iClass = new pgImageCatalogClass(dataset as IRasterDataset, this, fc, sRef, imageSpace);
                        ((pgImageCatalogClass)iClass).SpatialReference = sRef;
                    }

                    layers.Add(new DatasetElement(iClass));
                }
            }

            foreach (DataRow row in ds.Tables[0].Rows)
            {
                DataRow fcRow = row;

                if (IsLinkedFeatureClass(row))
                {
                    IDataset linkedDs = await LinkedDataset(LinkedDatasetCacheInstance, LinkedDatasetId(row));
                    if (linkedDs == null)
                    {
                        continue;
                    }

                    IDatasetElement linkedElement = await linkedDs.Element((string)row["Name"]);

                    LinkedFeatureClass fc = new LinkedFeatureClass(dataset,
                        linkedElement != null && linkedElement.Class is IFeatureClass ? linkedElement.Class as IFeatureClass : null,
                        (string)row["Name"]);

                    pgDatasetElement linkedLayer = new pgDatasetElement(fc);
                    layers.Add(linkedLayer);

                    continue;
                }

                if (row["Name"].ToString().Contains("@"))  // Geographic View
                {
                    string[] viewNames = row["Name"].ToString().Split('@');
                    if (viewNames.Length != 2)
                    {
                        continue;
                    }

                    DataRow[] fcRows = ds.Tables[0].Select("Name='" + viewNames[0] + "'");
                    if (fcRows == null || fcRows.Length != 1)
                    {
                        continue;
                    }

                    fcRow = fcRows[0];
                }

                if (!await TableExists(FcTableName(fcRow["name"].ToString())) ||
                    !await CheckTablePrivilege(FcTableName(fcRow["name"].ToString())))
                {
                    continue;
                }
                //if (_seVersion != 0)
                //{
                //    _conn.ExecuteNoneQuery("execute LoadIndex '" + row["Name"].ToString() + "'");
                //}
                GeometryDef geomDef;
                if (fcRow.Table.Columns["hasz"] != null)
                {
                    geomDef = new GeometryDef((GeometryType)fcRow["geometrytype"], null, (bool)fcRow["hasz"]);
                }
                else
                {  // alte Version war immer 3D
                    geomDef = new GeometryDef((GeometryType)fcRow["geometrytype"], null, true);
                }

                pgDatasetElement layer = await pgDatasetElement.Create(this, dataset, row["name"].ToString(), geomDef);
                if (layer.Class is pgFeatureClass)
                {
                    ((pgFeatureClass)layer.Class).Envelope = await this.FeatureClassExtent(layer.Class.Name);
                    ((pgFeatureClass)layer.Class).IDFieldName = "FDB_OID";
                    ((pgFeatureClass)layer.Class).ShapeFieldName = "FDB_SHAPE";
                    if (sRef != null)
                    {
                        ((pgFeatureClass)layer.Class).SpatialReference = (ISpatialReference)(new SpatialReference((SpatialReference)sRef));
                    }
                }
                var fields = await this.FeatureClassFields(dataset.DatasetName, layer.Class.Name);
                if (fields != null && layer.Class is ITableClass)
                {
                    foreach (IField field in fields)
                    {
                        ((FieldCollection)((ITableClass)layer.Class).Fields).Add(field);
                    }
                }
                layers.Add(layer);
            }
            return layers;
        }

        public override string EscapeQueryValue(string value)
        {
            return value;
        }

        #endregion

        #region IFeatureUpdater
        public override Task<bool> Insert(IFeatureClass fClass, IFeature feature)
        {
            List<IFeature> features = new List<IFeature>();
            features.Add(feature);
            return Insert(fClass, features);
        }

        async public override Task<bool> Insert(IFeatureClass fClass, List<IFeature> features)
        {
            if (fClass == null || features == null || !(fClass.Dataset is IFDBDataset))
            {
                return false;
            }

            if (features.Count == 0)
            {
                return true;
            }

            BinarySearchTree2 tree = null;
            bool isNetwork = fClass.GeometryType == GeometryType.Network;

            await CheckSpatialSearchTreeVersion(fClass.Name);
            if (_spatialSearchTrees[fClass.Name] == null)
            {
                _spatialSearchTrees[fClass.Name] = await this.SpatialSearchTree(fClass.Name);
            }
            tree = _spatialSearchTrees[fClass.Name] as BinarySearchTree2;

            var allowFcEditing = await Replication.AllowFeatureClassEditing(fClass);
            string replicationField = allowFcEditing.replFieldName;
            if (!allowFcEditing.allow)
            {
                _errMsg = "Replication Error: can't edit checked out and released featureclass...";
                return false;
            }
            try
            {
                //List<long> _nids = new List<long>();
                DbProviderFactory factory = DataProvider.PostgresProvider;
                using (DbConnection connection = factory.CreateConnection())
                {
                    connection.ConnectionString = _conn.ConnectionString;
                    await connection.OpenAsync();

                    using (DbCommand command = factory.CreateCommand())
                    using (DbTransaction transaction = connection.BeginTransaction())
                    {
                        command.Connection = connection;
                        ReplicationTransaction replTrans = null;// new ReplicationTransaction(connection, transaction);
                        command.Transaction = transaction;

                        StringBuilder fields = new StringBuilder();
                        StringBuilder parameterLines = new StringBuilder();

                        int count = 0;

                        #region Fields

                        fields.Append(DbColName(fClass.ShapeFieldName));

                        List<IField> dbFields = new List<IField>();
                        for (int i = 0; i < fClass.Fields.Count; i++)
                        {
                            var field = fClass.Fields[i];

                            if (field.type == FieldType.Shape || field.type == FieldType.ID || field.name.Equals("FDB_NID", StringComparison.InvariantCultureIgnoreCase))
                            {
                                continue;
                            }

                            dbFields.Add(field);
                            fields.Append(",");
                            fields.Append(DbColName(field.name));
                        }

                        bool hasNID = features.Where(f => f.FindField("$FDB_NID") != null).FirstOrDefault() != null;
                        fields.Append("," + DbColName("FDB_NID"));

                        #endregion

                        foreach (IFeature feature in features)
                        {
                            if (!await feature.BeforeInsert(fClass))
                            {
                                _errMsg = "Insert: Error in Feature.BeforeInsert (AutoFields,...)";
                                return false;
                            }
                            if (!String.IsNullOrEmpty(replicationField))
                            {
                                Replication.AllocateNewObjectGuid(feature, replicationField);
                                if (!await Replication.WriteDifferencesToTable(fClass, (System.Guid)feature[replicationField], Replication.SqlStatement.INSERT, replTrans))
                                {
                                    return false;
                                }
                            }

                            StringBuilder parameters = new StringBuilder();

                            DbParameter shapeParameter = factory.CreateParameter();
                            shapeParameter.ParameterName = "@fdb_shape" + count;
                            parameters.Append(shapeParameter.ParameterName);
                            command.Parameters.Add(shapeParameter);

                            if (feature.Shape != null)
                            {
                                var shape = fClass.ConvertTo(feature.Shape);
                                GeometryDef.VerifyGeometryType(shape, fClass);

                                BinaryWriter writer = new BinaryWriter(new MemoryStream());
                                shape.Serialize(writer, fClass);

                                byte[] geometry = new byte[writer.BaseStream.Length];
                                writer.BaseStream.Position = 0;
                                writer.BaseStream.Read(geometry, 0, (int)writer.BaseStream.Length);
                                writer.Close();

                                shapeParameter.Value = geometry;
                            }
                            else
                            {
                                shapeParameter.Value = DBNull.Value;
                            }

                            if (feature.Fields != null)
                            {

                                foreach (var dbField in dbFields)
                                {
                                    var fv = feature.Fields.Where(f => f.Name.Equals(dbField.name)).FirstOrDefault();

                                    DbParameter parameter = factory.CreateParameter();
                                    parameter.ParameterName = "@" + dbField.name + count;
                                    command.Parameters.Add(parameter);

                                    if (parameters.Length != 0)
                                    {
                                        parameters.Append(",");
                                    }
                                    parameters.Append(parameter.ParameterName);


                                    if (fv == null)
                                    {
                                        parameter.Value = dbField.DefautValue ?? System.DBNull.Value;
                                    }
                                    else
                                    {
                                        if (fv.Name == "FDB_NID" || fv.Name == "FDB_SHAPE" || fv.Name == "FDB_OID")
                                        {
                                            continue;
                                        }

                                        string name = fv.Name;

                                        parameter.Value = fv.Value != null ?
                                            ((fv.Value is Enum) ? (int)fv.Value : fv.Value) :
                                            System.DBNull.Value;
                                    }
                                }
                            }

                            DbParameter fdbNidparameter = factory.CreateParameter();
                            fdbNidparameter.ParameterName = "@fdb_nid" + count;
                            if (parameters.Length != 0)
                            {
                                parameters.Append(",");
                            }
                            parameters.Append(fdbNidparameter.ParameterName);
                            command.Parameters.Add(fdbNidparameter);

                            var fdbNidField = feature.FindField("$FDB_NID");
                            if (fdbNidField != null)
                            {
                                fdbNidparameter.Value = fdbNidField.Value;
                            }
                            if (!hasNID && isNetwork == false)
                            {
                                long NID = 0;
                                if (tree != null && feature.Shape != null)
                                {
                                    NID = tree.InsertSINode(feature.Shape.Envelope);
                                }

                                fdbNidparameter.Value = NID;
                            }

                            if (count > 0)
                            {
                                parameterLines.Append(",");
                            }
                            parameterLines.Append("(" + parameters + ")");

                            count++;
                        }

                        command.CommandText = "INSERT INTO " + FcTableName(fClass) + " (" + fields.ToString() + ") VALUES " + parameterLines;
                        await command.ExecuteNonQueryAsync();

                        transaction.Commit();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errMsg = "Insert: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace;
                return false;
            }
        }

        async public /*override*/ Task<bool> Insert_old(IFeatureClass fClass, List<IFeature> features)
        {
            if (fClass == null || features == null || !(fClass.Dataset is IFDBDataset))
            {
                return false;
            }

            if (features.Count == 0)
            {
                return true;
            }

            BinarySearchTree2 tree = null;
            bool isNetwork = fClass.GeometryType == GeometryType.Network;

            await CheckSpatialSearchTreeVersion(fClass.Name);
            if (_spatialSearchTrees[fClass.Name] == null)
            {
                _spatialSearchTrees[fClass.Name] = await this.SpatialSearchTree(fClass.Name);
            }
            tree = _spatialSearchTrees[fClass.Name] as BinarySearchTree2;

            var allowFcEditing = await Replication.AllowFeatureClassEditing(fClass);
            string replicationField = allowFcEditing.replFieldName;
            if (!allowFcEditing.allow)
            {
                _errMsg = "Replication Error: can't edit checked out and released featureclass...";
                return false;
            }
            try
            {
                //List<long> _nids = new List<long>();
                DbProviderFactory factory = DataProvider.PostgresProvider;
                using (DbConnection connection = factory.CreateConnection())
                {
                    connection.ConnectionString = _conn.ConnectionString;
                    await connection.OpenAsync();

                    using (DbCommand command = factory.CreateCommand())
                    using (DbTransaction transaction = connection.BeginTransaction())
                    {
                        command.Connection = connection;
                        ReplicationTransaction replTrans = null;// new ReplicationTransaction(connection, transaction);
                        command.Transaction = transaction;

                        StringBuilder sql = new StringBuilder();

                        foreach (IFeature feature in features)
                        {
                            if (!await feature.BeforeInsert(fClass))
                            {
                                _errMsg = "Insert: Error in Feature.BeforeInsert (AutoFields,...)";
                                return false;
                            }
                            if (!String.IsNullOrEmpty(replicationField))
                            {
                                Replication.AllocateNewObjectGuid(feature, replicationField);
                                if (!await Replication.WriteDifferencesToTable(fClass, (System.Guid)feature[replicationField], Replication.SqlStatement.INSERT, replTrans))
                                {
                                    return false;
                                }
                            }

                            StringBuilder fields = new StringBuilder(), parameters = new StringBuilder();
                            command.Parameters.Clear();
                            if (feature.Shape != null)
                            {
                                var shape = fClass.ConvertTo(feature.Shape);
                                GeometryDef.VerifyGeometryType(shape, fClass);

                                BinaryWriter writer = new BinaryWriter(new MemoryStream());
                                shape.Serialize(writer, fClass);

                                byte[] geometry = new byte[writer.BaseStream.Length];
                                writer.BaseStream.Position = 0;
                                writer.BaseStream.Read(geometry, (int)0, (int)writer.BaseStream.Length);
                                writer.Close();

                                DbParameter parameter = factory.CreateParameter();
                                parameter.ParameterName = "@fdb_shape";
                                parameter.Value = geometry;
                                fields.Append(DbColName("FDB_SHAPE"));
                                //if (feature.Shape is IPolygon)
                                parameters.Append("@fdb_shape");
                                command.Parameters.Add(parameter);
                            }

                            bool hasNID = false;
                            if (fClass.Fields != null)
                            {
                                foreach (IFieldValue fv in feature.Fields)
                                {
                                    if (fv.Name == "FDB_NID" || fv.Name == "FDB_SHAPE" || fv.Name == "FDB_OID")
                                    {
                                        continue;
                                    }

                                    string name = fv.Name.Replace("$", String.Empty);
                                    if (name == "FDB_NID")
                                    {
                                        hasNID = true;
                                    }
                                    else if (fClass.FindField(name) == null)
                                    {
                                        continue;
                                    }

                                    if (fields.Length != 0)
                                    {
                                        fields.Append(",");
                                    }

                                    if (parameters.Length != 0)
                                    {
                                        parameters.Append(",");
                                    }

                                    DbParameter parameter = factory.CreateParameter();
                                    parameter.ParameterName = "@" + name;
                                    parameter.Value = fv.Value != null ?
                                        ((fv.Value is Enum) ? (int)fv.Value : fv.Value) :
                                        System.DBNull.Value;
                                    fields.Append(DbColName(name));
                                    parameters.Append("@" + name);
                                    command.Parameters.Add(parameter);
                                }
                            }

                            if (!hasNID && isNetwork == false)
                            {
                                long NID = 0;
                                if (tree != null && feature.Shape != null)
                                {
                                    NID = tree.InsertSINode(feature.Shape.Envelope);
                                }

                                if (fields.Length != 0)
                                {
                                    fields.Append(",");
                                }

                                if (parameters.Length != 0)
                                {
                                    parameters.Append(",");
                                }

                                DbParameter parameter = factory.CreateParameter();
                                parameter.ParameterName = "@fdb_nid";
                                parameter.Value = NID;
                                fields.Append(DbColName("FDB_NID"));
                                parameters.Append("@fdb_nid");
                                command.Parameters.Add(parameter);
                            }

                            //command.CommandText = "INSERT INTO " + FcTableName(fClass) + " (" + fields.ToString() + ") VALUES (" + parameters + ")";
                            //await command.ExecuteNonQueryAsync();

                            if (sql.Length > 0)
                            {
                                sql.Append(";");
                            }
                            sql.Append("INSERT INTO " + FcTableName(fClass) + " (" + fields.ToString() + ") VALUES (" + parameters + ")");

                        }

                        command.CommandText = sql.ToString();
                        await command.ExecuteNonQueryAsync();
                        transaction.Commit();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errMsg = "Insert: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace;
                return false;
            }
        }

        public override Task<bool> Update(IFeatureClass fClass, IFeature feature)
        {
            List<IFeature> features = new List<IFeature>();
            features.Add(feature);
            return Update(fClass, features);
        }
        async public override Task<bool> Update(IFeatureClass fClass, List<IFeature> features)
        {
            if (fClass == null || features == null || !(fClass.Dataset is IFDBDataset))
            {
                return false;
            }

            if (features.Count == 0)
            {
                return true;
            }

            BinarySearchTree2 tree = null;

            await CheckSpatialSearchTreeVersion(fClass.Name);
            if (_spatialSearchTrees[fClass.Name] == null)
            {
                _spatialSearchTrees[fClass.Name] = await this.SpatialSearchTree(fClass.Name);
            }
            tree = _spatialSearchTrees[fClass.Name] as BinarySearchTree2;

            var allowFcEditing = await Replication.AllowFeatureClassEditing(fClass);
            string replicationField = allowFcEditing.replFieldName;
            if (!allowFcEditing.allow)
            {
                _errMsg = "Replication Error: can't edit checked out and released featureclass...";
                return false;
            }
            try
            {
                //List<long> _nids = new List<long>();

                using (DbConnection connection = _dbProviderFactory.CreateConnection())
                {
                    connection.ConnectionString = _conn.ConnectionString;
                    await connection.OpenAsync();

                    using (DbCommand command = _dbProviderFactory.CreateCommand())
                    using (DbTransaction transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        command.Connection = connection;
                        ReplicationTransaction replTrans = new ReplicationTransaction(connection, transaction);
                        command.Transaction = transaction;

                        foreach (IFeature feature in features)
                        {
                            if (!await feature.BeforeUpdate(fClass))
                            {
                                _errMsg = "Insert: Error in Feature.BeforeInsert (AutoFields,...)";
                                return false;
                            }
                            if (!String.IsNullOrEmpty(replicationField))
                            {
                                if (!await Replication.WriteDifferencesToTable(fClass,
                                    await Replication.FeatureObjectGuid(fClass, feature, replicationField),
                                    Replication.SqlStatement.UPDATE, replTrans))
                                {
                                    return false;
                                }
                            }

                            long NID = 0;
                            StringBuilder commandText = new StringBuilder();

                            if (feature == null)
                            {
                                continue;
                            }

                            if (feature.OID < 0)
                            {
                                _errMsg = "Can't update feature with OID=" + feature.OID;
                                return false;
                            }

                            StringBuilder fields = new StringBuilder();
                            command.Parameters.Clear();
                            if (feature.Shape != null)
                            {
                                var shape = fClass.ConvertTo(feature.Shape);
                                GeometryDef.VerifyGeometryType(shape, fClass);

                                BinaryWriter writer = new BinaryWriter(new MemoryStream());
                                shape.Serialize(writer, fClass);

                                byte[] geometry = new byte[writer.BaseStream.Length];
                                writer.BaseStream.Position = 0;
                                writer.BaseStream.Read(geometry, (int)0, (int)writer.BaseStream.Length);
                                writer.Close();

                                DbParameter parameter = _dbProviderFactory.CreateParameter();
                                parameter.ParameterName = "@fdb_shape";
                                parameter.Value = geometry;
                                fields.Append(DbColName("FDB_SHAPE") + "=@fdb_shape");
                                command.Parameters.Add(parameter);
                            }

                            if (fClass.Fields != null)
                            {
                                foreach (IFieldValue fv in feature.Fields)
                                {
                                    if (fv.Name == "FDB_OID" || fv.Name == "FDB_SHAPE")
                                    {
                                        continue;
                                    }

                                    if (fv.Name == "$FDB_NID")
                                    {
                                        long.TryParse(fv.Value.ToString(), out NID);
                                        continue;
                                    }
                                    string name = fv.Name;
                                    if (fClass.FindField(name) == null)
                                    {
                                        continue;
                                    }

                                    if (fields.Length != 0)
                                    {
                                        fields.Append(",");
                                    }

                                    DbParameter parameter = _dbProviderFactory.CreateParameter();
                                    parameter.ParameterName = "@" + name;
                                    parameter.Value = fv.Value != null ? fv.Value : System.DBNull.Value;
                                    fields.Append(DbColName(name) + "=@" + name);
                                    command.Parameters.Add(parameter);
                                }
                            }
                            // Wenn Shape upgedatet wird, auch neuen TreeNode berechnen
                            if (feature.Shape != null)
                            {
                                NID = (NID != 0) ? tree.UpdadeSINode(feature.Shape.Envelope, NID) : tree.InsertSINode(feature.Shape.Envelope);

                                if (fields.Length != 0)
                                {
                                    fields.Append(",");
                                }

                                DbParameter parameterNID = _dbProviderFactory.CreateParameter();
                                parameterNID.ParameterName = "@fdb_nid";
                                parameterNID.Value = NID;
                                fields.Append(DbColName("FDB_NID") + "=@fdb_nid");
                                command.Parameters.Add(parameterNID);
                            }

                            commandText.Append("UPDATE " + FcTableName(fClass) + " SET " + fields.ToString() + " WHERE \"FDB_OID\"=" + feature.OID);
                            command.CommandText = commandText.ToString();
                            await command.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errMsg = "Update: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace;
                return false;
            }
        }

        public override Task<bool> Delete(IFeatureClass fClass, int oid)
        {
            return Delete(fClass, DbColName("FDB_OID") + "=" + oid.ToString());
        }
        async public override Task<bool> Delete(IFeatureClass fClass, string where)
        {
            if (fClass == null)
            {
                return false;
            }

            try
            {
                using (DbConnection connection = _dbProviderFactory.CreateConnection())
                {
                    connection.ConnectionString = _conn.ConnectionString;
                    connection.Open();

                    string sql = "DELETE FROM " + FcTableName(fClass) + ((where != String.Empty) ? " WHERE " + where : "");
                    using (DbCommand command = _dbProviderFactory.CreateCommand())
                    using (DbTransaction transaction = connection.BeginTransaction())
                    {
                        command.CommandText = sql;
                        command.Connection = connection;
                        ReplicationTransaction replTrans = new ReplicationTransaction(connection, transaction);
                        command.Transaction = transaction;

                        var allowFcEditing = await Replication.AllowFeatureClassEditing(fClass);
                        string replicationField = allowFcEditing.replFieldName;
                        if (!allowFcEditing.allow)
                        {
                            _errMsg = "Replication Error: can't edit checked out and released featureclass...";
                            return false;
                        }
                        if (!String.IsNullOrEmpty(replicationField))
                        {
                            DataTable tab = await _conn.Select(DbColName(replicationField), FcTableName(fClass), ((where != String.Empty) ? where : ""));
                            if (tab == null)
                            {
                                _errMsg = "Replication Error: " + _conn.errorMessage;
                                return false;
                            }

                            foreach (DataRow row in tab.Rows)
                            {
                                if (!await Replication.WriteDifferencesToTable(fClass,
                                    (System.Guid)row[replicationField],
                                    Replication.SqlStatement.DELETE,
                                    replTrans))
                                {
                                    return false;
                                }
                            }
                        }

                        await command.ExecuteNonQueryAsync();
                        transaction.Commit();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
        }
        #endregion

        #region Table, Column Naming Overrides
        override public string TableName(string tableName)
        {
            string dbSchema = GetTableDbSchema(tableName);

            string pgTableName = (tableName != tableName.ToLower() && !tableName.Contains("\"")) ? "\"" + tableName + "\"" : tableName;
            return ((String.IsNullOrEmpty(dbSchema) || pgTableName.Contains(".")) ? pgTableName : dbSchema + "." + pgTableName);
        }
        override public string DbColName(string fieldName)
        {
            return (fieldName != fieldName.ToLower()) ? "\"" + fieldName + "\"" : fieldName;
        }
        override public string ColumnName(string fieldName)
        {
            return fieldName;
        }
        override protected string FcTableName(string fcName)
        {
            string dbSchema = GetFeatureClassDbSchema(fcName);
            return (String.IsNullOrEmpty(dbSchema) ? "\"FC_" + fcName + "\"" : dbSchema + "." + "\"FC_" + fcName + "\"");
        }
        override protected string FcsiTableName(string fcName)
        {
            string dbSchema = GetFeatureClassDbSchema(fcName);
            return (String.IsNullOrEmpty(dbSchema) ? "\"FCSI_" + fcName + "\"" : dbSchema + "." + "\"FCSI_" + fcName + "\"");
        }
        override protected string FcTableName(IFeatureClass fc)
        {
            return FcTableName(fc.Name);
        }
        override protected string FcsiTableName(IFeatureClass fc)
        {
            return FcsiTableName(fc.Name);
        }
        #endregion

        #region BinaryTree2

        #region Rebuild Spatial Index
        protected override bool UpdateFeatureSpatialNodeID(IFeatureClass fc, int oid, long nid)
        {
            if (fc == null || _conn == null)
            {
                return false;
            }

            try
            {
                using (DbConnection connection = _dbProviderFactory.CreateConnection())
                {
                    connection.ConnectionString = _conn.ConnectionString;
                    DbCommand command = _dbProviderFactory.CreateCommand();
                    command.CommandText = "UPDATE " + FcTableName(fc) + " SET " + DbColName("FDB_NID") + "=" + nid.ToString() + " WHERE " + DbColName(fc.IDFieldName) + "=" + oid.ToString();
                    command.Connection = connection;

                    connection.Open();
                    command.ExecuteNonQuery();
                    connection.Close();
                }
                return true;
            }
            catch (Exception ex)
            {
                _errMsg = "Fatal Exception:" + ex.Message;
                return false;
            }
        }
        #endregion

        #endregion

        #region Replication
        override public bool InsertRow(string table, IRow row, IReplicationTransaction replTrans)
        {
            try
            {
                DbCommand command = _dbProviderFactory.CreateCommand();

                StringBuilder fields = new StringBuilder(), parameters = new StringBuilder();
                command.Parameters.Clear();

                foreach (IFieldValue fv in row.Fields)
                {
                    string name = fv.Name;

                    if (fields.Length != 0)
                    {
                        fields.Append(",");
                    }

                    if (parameters.Length != 0)
                    {
                        parameters.Append(",");
                    }

                    DbParameter parameter = _dbProviderFactory.CreateParameter();
                    parameter.ParameterName = "@" + name;
                    parameter.Value = fv.Value;

                    fields.Append(DbColName(name));
                    parameters.Append("@" + name);
                    command.Parameters.Add(parameter);
                }

                string tabname = TableName(table);
                command.CommandText = "INSERT INTO " + tabname + " (" + fields.ToString() + ") VALUES (" + parameters + ")";

                if (replTrans != null && replTrans.IsValid)
                {
                    replTrans.ExecuteNonQuery(command);
                }
                else
                {
                    using (DbConnection connection = _dbProviderFactory.CreateConnection())
                    {
                        connection.ConnectionString = _conn.ConnectionString;
                        connection.Open();
                        command.Connection = connection;
                        command.ExecuteNonQuery();
                        command.Dispose();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _errMsg = "Insert: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace;
                return false;
            }
        }
        override public bool InsertRows(string table, List<IRow> rows, IReplicationTransaction replTrans)
        {
            if (rows == null || rows.Count == 0)
            {
                return true;
            }

            try
            {
                bool useTrans = replTrans != null && replTrans.IsValid;
                using (DbConnection connection = _dbProviderFactory.CreateConnection())
                {
                    connection.ConnectionString = _conn.ConnectionString;

                    if (useTrans)
                    {
                        DbCommand command = _dbProviderFactory.CreateCommand();
                        foreach (IRow row in rows)
                        {
                            StringBuilder fields = new StringBuilder(), parameters = new StringBuilder();
                            command.Parameters.Clear();

                            foreach (IFieldValue fv in row.Fields)
                            {
                                string name = fv.Name;

                                if (fields.Length != 0)
                                {
                                    fields.Append(",");
                                }

                                if (parameters.Length != 0)
                                {
                                    parameters.Append(",");
                                }

                                DbParameter parameter = _dbProviderFactory.CreateParameter();
                                parameter.ParameterName = "@" + name;
                                parameter.Value = fv.Value;

                                fields.Append(DbColName(name));
                                parameters.Append("@" + name);
                                command.Parameters.Add(parameter);
                            }

                            string tabname = TableName(table);

                            command.CommandText = "INSERT INTO " + tabname + " (" + fields.ToString() + ") VALUES (" + parameters + ")";

                            replTrans.ExecuteNonQuery(command);
                        }

                        command.Dispose();
                    }
                    else
                    {
                        using (DbTransaction transaction = connection.BeginTransaction())
                        using (DbCommand command = connection.CreateCommand())
                        {
                            command.Connection = connection;
                            connection.Open();

                            foreach (IRow row in rows)
                            {
                                StringBuilder fields = new StringBuilder(), parameters = new StringBuilder();
                                command.Parameters.Clear();

                                foreach (IFieldValue fv in row.Fields)
                                {
                                    string name = fv.Name;

                                    if (fields.Length != 0)
                                    {
                                        fields.Append(",");
                                    }

                                    if (parameters.Length != 0)
                                    {
                                        parameters.Append(",");
                                    }

                                    DbParameter parameter = _dbProviderFactory.CreateParameter();
                                    parameter.ParameterName = "@" + name;
                                    parameter.Value = fv.Value;

                                    fields.Append(DbColName(name));
                                    parameters.Append("@" + name);
                                    command.Parameters.Add(parameter);
                                }

                                string tabname = TableName(table);

                                command.CommandText = "INSERT INTO " + tabname + " (" + fields.ToString() + ") VALUES (" + parameters + ")";
                                command.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _errMsg = "Insert: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace;
                return false;
            }
        }
        override public bool UpdateRow(string table, IRow row, string IDField, IReplicationTransaction replTrans)
        {
            try
            {
                DbCommand command = _dbProviderFactory.CreateCommand();
                command.Parameters.Clear();
                StringBuilder commandText = new StringBuilder();

                if (row.OID < 0)
                {
                    _errMsg = "Can't update feature with OID=" + row.OID;
                    return false;
                }

                StringBuilder fields = new StringBuilder();
                foreach (IFieldValue fv in row.Fields)
                {
                    string name = fv.Name;
                    if (fields.Length != 0)
                    {
                        fields.Append(",");
                    }

                    DbParameter parameter = _dbProviderFactory.CreateParameter();
                    parameter.ParameterName = "@" + name;
                    parameter.Value = fv.Value;

                    fields.Append(DbColName(name) + "=@" + name);
                    command.Parameters.Add(parameter);
                }

                string tabname = TableName(table);

                commandText.Append("UPDATE " + tabname + " SET " + fields.ToString() + " WHERE " + DbColName(IDField) + "=" + row.OID);
                command.CommandText = commandText.ToString();

                if (replTrans != null && replTrans.IsValid)
                {
                    replTrans.ExecuteNonQuery(command);
                }
                else
                {
                    using (DbConnection connection = _dbProviderFactory.CreateConnection())
                    {
                        connection.ConnectionString = _conn.ConnectionString;
                        connection.Open();
                        command.Connection = connection;
                        command.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _errMsg = "Update: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace;
                return false;
            }
        }
        override public bool DeleteRows(string table, string where, IReplicationTransaction replTrans)
        {
            try
            {
                string sql = "DELETE FROM " + TableName(table) + ((where != String.Empty) ? " WHERE " + where : "");
                DbCommand command = _dbProviderFactory.CreateCommand();
                command.CommandText = sql;

                if (replTrans != null && replTrans.IsValid)
                {
                    replTrans.ExecuteNonQuery(command);
                }
                else
                {
                    using (DbConnection connection = _dbProviderFactory.CreateConnection())
                    {
                        connection.ConnectionString = _conn.ConnectionString;
                        connection.Open();
                        command.Connection = connection;

                        command.ExecuteNonQuery();
                        connection.Close();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
        }

        override public DbProviderFactory ProviderFactory
        {
            get
            {
                return _dbProviderFactory;
            }
        }
        public override string GuidToSql(Guid guid)
        {
            return "'" + guid.ToString() + "'";
        }

        public override bool IsFilebaseDatabase
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region IAltertable

        public new event AlterTableEventHandler TableAltered;

        async public override Task<bool> AlterTable(string table, IField oldField, IField newField)
        {
            if (oldField == null && newField == null)
            {
                return true;
            }

            if (_conn == null)
            {
                return false;
            }

            int dsID = await DatasetIDFromFeatureClassName(table);
            string dsname = await DatasetNameFromFeatureClassName(table);
            if (dsname == "")
            {
                return false;
            }
            int fcID = await FeatureClassID(dsID, table);
            if (fcID == -1)
            {
                return false;
            }

            IFeatureClass fc = await GetFeatureclass(dsname, table);

            if (fc == null || fc.Fields == null)
            {
                return false;
            }

            try
            {
                if (oldField != null)
                {
                    if (fc.FindField(oldField.name) == null)
                    {
                        _errMsg = "Featureclass " + table + " do not contain field '" + oldField.name + "'";
                        return false;
                    }
                    if (oldField.Equals(newField))
                    {
                        return true;
                    }

                    if (newField != null)   // ALTER COLUMN
                    {
                        string sql = "ALTER TABLE " + FcTableName(table) +
                            " ALTER COLUMN " + DbColName(oldField.name) + " " + FieldDataType(newField);

                        if (!_conn.ExecuteNoneQuery(sql))
                        {
                            _errMsg = _conn.errorMessage;
                            return false;
                        }
                        if (oldField.name != newField.name)
                        {
                            if (!await RenameField(table, oldField, newField))
                            {
                                return false;
                            }
                        }
                    }
                    else    // DROP COLUMN
                    {
                        string replIDFieldname = await Replication.FeatureClassReplicationIDFieldname(fc);
                        if (!String.IsNullOrEmpty(replIDFieldname) &&
                            replIDFieldname == oldField.name)
                        {
                            Replication repl = new Replication();
                            if (!await repl.RemoveReplicationIDField(fc))
                            {
                                _errMsg = "Can't remove replication id field...";
                                return false;
                            }
                        }

                        string sql = "ALTER TABLE " + FcTableName(table) +
                            " DROP COLUMN " + DbColName(oldField.name);

                        if (!_conn.ExecuteNoneQuery(sql))
                        {
                            _errMsg = _conn.errorMessage;
                            return false;
                        }
                    }
                }
                else  // ADD COLUMN
                {
                    string sql = "ALTER TABLE " + FcTableName(table) +
                        " ADD COLUMN " + DbColName(newField.name) + " " + FieldDataType(newField);

                    if (!_conn.ExecuteNoneQuery(sql))
                    {
                        _errMsg = _conn.errorMessage;
                        return false;
                    }
                }

                return await AlterFeatureclassField(fcID, oldField, newField);
            }
            finally
            {
                if (TableAltered != null)
                {
                    TableAltered(table);
                }
            }
        }
        #endregion

        #region Helper Classes

        internal class pgDatasetElement : gView.Framework.Data.DatasetElement, IFeatureSelection
        {
            private ISelectionSet m_selectionset;

            private pgDatasetElement()
            {

            }

            async static public Task<pgDatasetElement> Create(pgFDB fdb, IDataset dataset, string name, GeometryDef geomDef)
            {
                var dsElement = new pgDatasetElement();

                if (geomDef.GeometryType == GeometryType.Network)
                {
                    dsElement._class = await pgNetworkFeatureClass.Create(fdb, dataset, name, geomDef);
                }
                else
                {
                    dsElement._class = await pgFeatureClass.Create(fdb, dataset, geomDef);
                    ((pgFeatureClass)dsElement._class).Name =
                    ((pgFeatureClass)dsElement._class).Aliasname = name;
                }
                dsElement.Title = name;

                return dsElement;
            }

            public pgDatasetElement(LinkedFeatureClass fc)
            {
                _class = fc;
                this.Title = fc.Name;
            }

            #region IFeatureSelection Member

            public ISelectionSet SelectionSet
            {
                get
                {
                    return (ISelectionSet)m_selectionset;
                }
                set
                {
                    if (m_selectionset != null && m_selectionset != value)
                    {
                        m_selectionset.Clear();
                    }

                    m_selectionset = value;
                }
            }

            async public Task<bool> Select(IQueryFilter filter, CombinationMethod methode)
            {
                if (!(this.Class is ITableClass))
                {
                    return false;
                }

                ISelectionSet selSet = await ((ITableClass)this.Class).Select(filter);

                SelectionSet = selSet;
                FireSelectionChangedEvent();

                return true;
            }

            public event FeatureSelectionChangedEvent FeatureSelectionChanged;
            public event BeforeClearSelectionEvent BeforeClearSelection;

            public void ClearSelection()
            {
                if (m_selectionset != null)
                {
                    BeforeClearSelection?.Invoke(this);

                    m_selectionset.Clear();
                    m_selectionset = null;
                    FireSelectionChangedEvent();
                }
            }

            public void FireSelectionChangedEvent()
            {
                FeatureSelectionChanged?.Invoke(this);
            }

            #endregion
        }

        #endregion

        #region Cursors

        internal class pgFeatureCursor : FeatureCursor
        {
            DbProviderFactory _factory;
            DbConnection _connection;
            DbDataReader _reader;
            DbCommand _command;

            IGeometryDef _geomDef;
            string _sql = String.Empty, _where = String.Empty, _orderBy = String.Empty;
            bool _nolock = false;
            int _nid_pos = 0;
            List<long> _nids;
            ISpatialFilter _spatialFilter;
            int _limit = 0, _beginrecord = 0;

            public pgFeatureCursor(IGeometryDef geomDef, ISpatialReference toSRef) :
                base((geomDef != null) ? geomDef.SpatialReference : null,
                     toSRef)
            {

            }

            async static public Task<IFeatureCursor> Create(string connString, string sql, string where, string orderBy, int limit, int beginRecord, bool nolock, List<long> nids, ISpatialFilter filter, IGeometryDef geomDef, ISpatialReference toSRef)
            {
                var cursor = new pgFeatureCursor(geomDef, toSRef);

                try
                {
                    cursor._factory = pgFDB._dbProviderFactory;
                    cursor._connection = cursor._factory.CreateConnection();
                    cursor._connection.ConnectionString = connString;
                    cursor._command = cursor._factory.CreateCommand();
                    cursor._command.CommandText = cursor._sql = sql;
                    cursor._command.Connection = cursor._connection;
                    cursor._limit = limit;
                    cursor._beginrecord = beginRecord;
                    await cursor._connection.OpenAsync();

                    cursor._geomDef = geomDef;
                    cursor._where = where;
                    cursor._orderBy = orderBy;
                    cursor._nolock = nolock;
                    if (nids != null)
                    {
                        /*if (nids.Count > 0)*/
                        cursor._nids = nids;
                    }

                    cursor._spatialFilter = filter;

                    await cursor.ExecuteReaderAsync();
                }
                catch (Exception)
                {
                    cursor.Dispose();
                    throw;
                }

                return cursor;
            }

            async private Task<bool> ExecuteReaderAsync()
            {
                if (_reader != null)
                {
                    _reader.Close();
                    _reader = null;
                }

                string where = _where;
                _command = _factory.CreateCommand();
                _command.Connection = _connection;

                StringBuilder sb = new StringBuilder();
                List<DbParameter> parameters = new List<DbParameter>();
                if (_nids != null)
                {
                    int pIndex = 0;
                    StringBuilder where_nid = new StringBuilder();

                    if (_nid_pos >= _nids.Count)
                    {
                        return false;
                    }
                    if (_nids[_nid_pos] < 0)
                    {
                        if (where_nid.Length > 1)
                        {
                            where_nid.Append(" OR ");
                        }

                        where_nid.Append("(\"FDB_NID\" between @p" + (pIndex++) + " and @p" + (pIndex++) + ")");

                        DbParameter parameter1 = _factory.CreateParameter();
                        parameter1.ParameterName = "@p" + (pIndex - 2);
                        parameter1.DbType = DbType.Int64;
                        parameter1.Size = 8;
                        _command.Parameters.Add(parameter1);

                        DbParameter parameter2 = _factory.CreateParameter();
                        parameter2.ParameterName = "@p" + (pIndex - 1);
                        parameter2.DbType = DbType.Int64;
                        parameter2.Size = 8;
                        _command.Parameters.Add(parameter2);

                        _command.CommandText = GetCommandText(where_nid, where);
                        _command.Prepare(); // machts irgendwie schneller

                        parameter1.Value = -_nids[_nid_pos];
                        parameter2.Value = _nids[_nid_pos + 1];
                        _nid_pos++;
                    }
                    else
                    {
                        if (where_nid.Length > 1)
                        {
                            where_nid.Append(" OR ");
                        }

                        where_nid.Append("(\"FDB_NID\"=@p" + (pIndex++) + ")");

                        DbParameter parameter = _factory.CreateParameter();
                        parameter.ParameterName = "@p" + (pIndex - 1);
                        parameter.DbType = DbType.Int64;
                        parameter.Size = 8;
                        _command.Parameters.Add(parameter);

                        _command.CommandText = GetCommandText(where_nid, where);
                        _command.Prepare(); // machts irgendwie schneller

                        parameter.Value = _nids[_nid_pos];
                    }
                }
                else
                {
                    if (_nid_pos > 0)
                    {
                        return false;
                    }
                }
                _nid_pos++;

                if (String.IsNullOrEmpty(_command.CommandText))
                {
                    _command.CommandText = GetCommandText(null, where);
                }

                if (_limit > 0)
                {
                    _command.CommandText += " limit " + _limit;
                }
                if (_beginrecord > 0)
                {
                    _command.CommandText += " offset " + Math.Max(0, _beginrecord - 1);
                }

                _command.SetCustomCursorTimeout();
                _reader = await _command.ExecuteReaderAsync(CommandBehavior.Default);

                return true;
            }

            public override void Dispose()
            {
                base.Dispose();
                if (_connection != null && _command != null)
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        if (_reader != null)
                        {
                            try
                            {
                                while (_reader.Read())
                                {
                                    try { _reader.Close(); }
                                    catch { }
                                    _command.Cancel();
                                }
                            }
                            catch (Exception /*ex*/)
                            {

                            }
                        }
                        if (_command != null)
                        {
                            _command.Dispose();
                            _command = null;
                        }
                    }
                }

                if (_reader != null)
                {
                    try { _reader.Close(); }
                    catch { }
                    _reader = null;
                }
                if (_connection != null)
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        try { _connection.Close(); }
                        catch { }
                    }

                    _connection.Dispose();
                    _connection = null;
                }
            }

            #region IFeatureCursor Member

            public void Reset()
            {
            }

            public void Release()
            {
                this.Dispose();
            }

            async public override Task<IFeature> NextFeature()
            {
                try
                {
                    while (true)
                    {
                        if (_reader == null)
                        {
                            this.Dispose();
                            return null;
                        }
                        if (!await _reader.ReadAsync())
                        {
                            await this.ExecuteReaderAsync();
                            return await this.NextFeature();
                        }

                        Feature feature = new Feature();
                        for (int i = 0; i < _reader.FieldCount; i++)
                        {
                            string name = _reader.GetName(i);
                            object obj = _reader.GetValue(i);
                            if (name == "FDB_SHAPE" && obj != DBNull.Value)
                            {
                                BinaryReader r = new BinaryReader(new MemoryStream());
                                r.BaseStream.Write((byte[])obj, 0, ((byte[])obj).Length);
                                r.BaseStream.Position = 0;

                                IGeometry p = null;
                                switch (_geomDef.GeometryType)
                                {
                                    case GeometryType.Point:
                                        p = new gView.Framework.Geometry.Point();
                                        break;
                                    case GeometryType.Polyline:
                                        p = new gView.Framework.Geometry.Polyline();
                                        break;
                                    case GeometryType.Polygon:
                                        p = new gView.Framework.Geometry.Polygon();
                                        break;
                                }
                                if (p != null)
                                {
                                    p.Deserialize(r, _geomDef);

                                    r.Close();

                                    if (_spatialFilter != null)
                                    {
                                        if (!gView.Framework.Geometry.SpatialRelation.Check(_spatialFilter, p))
                                        {
                                            feature = null;
                                            break;
                                        }
                                    }
                                    feature.Shape = p;
                                }
                            }
                            else
                            {
                                FieldValue fv = new FieldValue(name, obj);
                                feature.Fields.Add(fv);
                                if (fv.Name == "FDB_OID")
                                {
                                    feature.OID = Convert.ToInt32(obj);
                                }
                            }
                        }
                        if (feature == null)
                        {
                            continue;
                        }

                        Transform(feature);
                        return feature;
                    }
                }
                catch (Exception)
                {
                    Dispose();
                    throw;
                    //return null;
                }
            }

            #endregion

            private string GetCommandText(StringBuilder where_nid, string where)
            {
                if (where_nid != null)
                {
                    where = where_nid.ToString() + (!String.IsNullOrEmpty(where) ? " AND (" + where + ")" : String.Empty);
                }

                if (where != String.Empty)
                {
                    where = " WHERE " + where;
                }

                return _sql + where + ((_orderBy != String.Empty) ? " ORDER BY " + _orderBy : String.Empty);
            }

            #region Helper

            #endregion
        }

        internal class pgFeatureCursorIDs : FeatureCursor
        {
            private DbProviderFactory _factory = pgFDB._dbProviderFactory;
            private DbConnection _connection;
            private DbDataReader _reader;
            private DbCommand _command;
            private IGeometryDef _geomDef;
            private List<int> _IDs;
            private int _id_pos = 0;
            private string _sql;

            private pgFeatureCursorIDs(IGeometryDef geomDef, ISpatialReference toSRef)
                : base((geomDef != null) ? geomDef.SpatialReference : null, toSRef)
            {

            }

            async public static Task<IFeatureCursor> Create(string connString, string sql, List<int> IDs, IGeometryDef geomDef, ISpatialReference toSRef)
            {
                var cursor = new pgFeatureCursorIDs(geomDef, toSRef);

                try
                {
                    cursor._connection = cursor._factory.CreateConnection();
                    cursor._connection.ConnectionString = connString;
                    await cursor._connection.OpenAsync();

                    cursor._sql = sql;
                    cursor._geomDef = geomDef;
                    cursor._IDs = IDs;

                    await cursor.ExecuteReaderAsync();
                }
                catch (Exception /*ex*/)
                {
                    cursor.Dispose();
                }

                return cursor;
            }

            public override void Dispose()
            {
                base.Dispose();
                if (_connection != null && _command != null)
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        if (_reader != null)
                        {
                            try
                            {
                                while (_reader.Read())
                                {
                                    try { _reader.Close(); }
                                    catch { }
                                    _command.Cancel();
                                }
                            }
                            catch (Exception)
                            {

                            }
                        }
                        if (_command != null)
                        {
                            _command.Dispose();
                            _command = null;
                        }
                    }
                }

                if (_reader != null)
                {
                    try { _reader.Close(); }
                    catch { }
                    _reader = null;
                }
                if (_connection != null)
                {
                    if (_connection.State == ConnectionState.Open)
                    {
                        _connection.Close();
                    }

                    _connection.Dispose();
                    _connection = null;
                }
            }

            async private Task<bool> ExecuteReaderAsync()
            {
                if (_reader != null)
                {
                    _reader.Close();
                    _reader = null;
                }
                if (_IDs == null)
                {
                    return false;
                }

                if (_id_pos >= _IDs.Count)
                {
                    return false;
                }

                int counter = 0;
                StringBuilder sb = new StringBuilder();
                while (true)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(",");
                    }

                    sb.Append(_IDs[_id_pos].ToString());
                    counter++;
                    _id_pos++;
                    if (_id_pos >= _IDs.Count || counter > 49)
                    {
                        break;
                    }
                }
                if (sb.Length == 0)
                {
                    return false;
                }

                string where = " WHERE \"FDB_OID\" IN (" + sb.ToString() + ")";

                _command = _factory.CreateCommand();
                _command.CommandText = _sql + where;
                _command.Connection = _connection;

                _reader = await _command.ExecuteReaderAsync(CommandBehavior.Default);

                return true;
            }

            #region IFeatureCursor Member

            public void Reset()
            {
            }

            public void Release()
            {
                this.Dispose();
            }

            async public override Task<IFeature> NextFeature()
            {
                try
                {
                    if (_reader == null)
                    {
                        return null;
                    }

                    if (!await _reader.ReadAsync())
                    {
                        await ExecuteReaderAsync();
                        return await NextFeature();
                    }

                    Feature feature = new Feature();
                    for (int i = 0; i < _reader.FieldCount; i++)
                    {
                        string name = _reader.GetName(i);
                        object obj = _reader.GetValue(i);
                        if (name == "FDB_SHAPE")
                        {
                            BinaryReader r = new BinaryReader(new MemoryStream());
                            r.BaseStream.Write((byte[])obj, 0, ((byte[])obj).Length);
                            r.BaseStream.Position = 0;

                            IGeometry p = null;
                            switch (_geomDef.GeometryType)
                            {
                                case GeometryType.Point:
                                    p = new gView.Framework.Geometry.Point();
                                    break;
                                case GeometryType.Polyline:
                                    p = new gView.Framework.Geometry.Polyline();
                                    break;
                                case GeometryType.Polygon:
                                    p = new gView.Framework.Geometry.Polygon();
                                    break;
                            }
                            if (p != null)
                            {
                                p.Deserialize(r, _geomDef);
                                r.Close();

                                feature.Shape = p;
                            }
                        }
                        else
                        {
                            FieldValue fv = new FieldValue(/*_schemaTable.Rows[i][0].ToString()*/name, obj);
                            feature.Fields.Add(fv);
                            if (fv.Name == "FDB_OID")
                            {
                                feature.OID = Convert.ToInt32(obj);
                            }
                        }
                    }

                    Transform(feature);
                    return feature;
                }
                catch
                {
                    Dispose();
                    return null;
                }
            }

            #endregion
        }
        #endregion
    }
}
