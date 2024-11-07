using gView.DataSources.Fdb.MSAccess;
using gView.Framework.Common;
using gView.Framework.Common.Extensions;
using gView.Framework.Core.Common;
using gView.Framework.Core.Data;
using gView.Framework.Core.Data.Cursors;
using gView.Framework.Core.FDB;
using gView.Framework.Data.Filters;
using gView.Framework.Db;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace gView.DataSources.Fdb.ImageDataset
{
    public class FDBImageDataset
    {
        public delegate void ReportActionEvent(FDBImageDataset sender, string action);
        public delegate void ReportProgressEvent(FDBImageDataset sender, int progress);
        //public delegate void ReportRequestEvent(FDBImageDataset sender, RequestArgs args);
        public event ReportActionEvent ReportAction = null;
        public event ReportProgressEvent ReportProgress = null;
        //public event ReportRequestEvent ReportRequest = null;

        private gView.Framework.Db.ICommonDbConnection _conn = null;
        private string _errMsg = String.Empty, _dsname = "";
        private IImageDB _fdb = null;
        private ICancelTracker _cancelTracker;

        public FDBImageDataset(IImageDB fdb, string dsname, ICancelTracker cancelTracker = null)
        {
            _fdb = fdb;
            _dsname = dsname;
            if (_fdb is AccessFDB)
            {
                _conn = ((AccessFDB)_fdb)._conn;
            }
            _cancelTracker = cancelTracker ?? new CancelTracker();
        }

        public string lastErrorMessage
        {
            get { return _errMsg; }
        }

        public ICancelTracker CancelTracker
        {
            get { return _cancelTracker; }
        }

        #region ImageDataset
        async public Task<bool> InsertImageDatasetBitmap(int image_id, IRasterLayer layer, int levels, System.Drawing.Imaging.ImageFormat format)
        {
            if (_conn == null)
            {
                _errMsg = "Not Connected (use Open...)";
                return false;
            }
            if (layer.RasterClass == null)
            {
                _errMsg = "No Rasterclass...";
                return false;
            }

            DataTable tab = await _conn.Select("*", "FDB_Datasets", "Name='" + _dsname + "' AND ImageDataset=1");
            if (tab == null)
            {
                _errMsg = _conn.errorMessage;
                return false;
            }
            if (tab.Rows.Count == 0)
            {
                _errMsg = "Can't find ImageDataset";
                return false;
            }
            string imageSpace = tab.Rows[0]["ImageSpace"].ToString();

            if (imageSpace.Equals("unmanaged"))
            {
                return true;
            }

            if (imageSpace.Equals(System.DBNull.Value) || imageSpace.Equals("database") || imageSpace.Equals(""))
            {
                _errMsg = "Not implemented (imageSpace==database)";
            }
            else
            {
                _errMsg = "Not implemented (imageSpace!=unmanaged)";
            }

            return false;
        }

        public Task<bool> UpdateImageDatasetBitmap(int image_id)
        {
            /*
            try
            {
                string filename = (string)await _conn.QuerySingleField("SELECT PATH FROM FC_" + _dsname + "_IMAGE_POLYGONS WHERE FDB_OID=" + image_id, "PATH");
                if (filename == null)
                {
                    _errMsg = "Can't find File with OID=" + image_id;
                    return false;
                }
                RasterFileLayer rLayer = new RasterFileLayer(null, filename);
                if (!rLayer.isValid)
                {
                    _errMsg = "No valid image file:" + filename;
                    return false;
                }

                // Geometrie updaten
                QueryFilter filter = new QueryFilter();
                filter.AddField("*");
                filter.WhereClause = "FDB_OID=" + image_id;
                IFeatureCursor cursor = ((IFDB)_fdb).Query(_dsname + "_IMAGE_POLYGONS", filter);
                if (cursor == null)
                {
                    _errMsg = "Can't query for polygon feature with OID=" + image_id;
                    return false;
                }
                IFeature feature = cursor.NextFeature;
                cursor.Release();
                if (feature == null)
                {
                    _errMsg = "No polygon feature in featureclass " + _dsname + "_IMAGE_POLYGONS with OID=" + image_id;
                    return false;
                }
                feature.Shape = rLayer.Polygon;
                FileInfo fi = new FileInfo(filename);
                feature["LAST_MODIFIED"] = fi.LastWriteTime;
                if (rLayer is IRasterFile && ((IRasterFile)rLayer).WorldFile != null)
                {
                    IRasterWorldFile world = ((IRasterFile)rLayer).WorldFile;
                    fi = new FileInfo(world.Filename);
                    if (fi.Exists)
                    {
                        feature["LAST_MODIFIED2"] = fi.LastWriteTime;
                        feature["PATH2"] = fi.FullName;
                    }
                    feature["CELLX"] = world.cellX;
                    feature["CELLY"] = world.cellY;
                }
                if (!((IFeatureUpdater)_fdb).Update(_dsname + "_IMAGE_POLYGONS", feature))
                {
                    _errMsg = ((IFDB)_fdb).lastErrorMsg;
                    return false;
                }

                ImageFormat format = ImageFormat.Jpeg;
                switch (feature["FORMAT"].ToString().ToLower())
                {
                    case "png":
                        format = ImageFormat.Png;
                        break;
                    case "tif":
                    case "tiff":
                        format = ImageFormat.Tiff;
                        break;
                }

                if (!DeleteImageDatasetItem(image_id, true))
                {
                    return false;
                }
                if (!InsertImageDatasetBitmap(image_id, rLayer, (int)feature["LEVELS"], format))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _errMsg = ex.Message;
                return false;
            }
            */
            _errMsg = "This method is not implemented any more...";
            return Task.FromResult(false);
        }

        async public Task<bool> DeleteImageDatasetItem(int image_id, bool bitmapOnly)
        {
            if (image_id == -1)
            {
                return false;
            }

            var isImageDatasetResult = await _fdb.IsImageDataset(_dsname);
            string imageSpace = isImageDatasetResult.imageSpace;
            if (!isImageDatasetResult.isImageDataset)
            {
                return false;
            }

            if (imageSpace.Equals(System.DBNull.Value))
            {
                imageSpace = "database";
            }

            DataTable tab = await _conn.Select("*", DataProvider.ToDbTableName(_conn.dbTypeString, "FC_" + _dsname + "_IMAGE_POLYGONS"), $"{DataProvider.ToDbFieldName(_conn.dbTypeString, "FDB_OID")}={image_id}", ""/*, ((bitmapOnly) ? false : true)*/);
            if (tab == null)
            {
                _errMsg = _conn.errorMessage;
                return false;
            }
            if (tab.Rows.Count != 1)
            {
                _errMsg = "Can't find Record with OID=" + image_id;
                return false;
            }
            DataRow row = tab.Rows[0];

            bool managed = (bool)row["MANAGED"];
            string managed_file = row["MANAGED_FILE"].ToString();

            if (!bitmapOnly)
            {
                row.Delete();
                if (!_conn.ExecuteNoneQuery($"DELETE FROM {DataProvider.ToDbTableName(_conn.dbTypeString, $"FC_{_dsname}_IMAGE_POLYGONS")} WHERE {DataProvider.ToDbFieldName(_conn.dbTypeString, "FDB_OID")}={image_id}"))
                //if (!_conn.Update(tab))
                {
                    _errMsg = _conn.errorMessage;
                    return false;
                }
            }
            if (!managed)
            {
                return true;
            }

            switch (imageSpace.ToLower())
            {
                case "":
                case "database":
                    if (!_conn.ExecuteNoneQuery($"DELETE FROM {DataProvider.ToDbTableName(_conn.dbTypeString, $"{_dsname}_IMAGE_DATA")} WHERE {DataProvider.ToDbFieldName(_conn.dbTypeString, "IMAGE_ID")}={image_id}"))
                    {
                        _errMsg = _conn.errorMessage;
                        return false;
                    }

                    //DataTable images = await _conn.Select("ID", _dsname + "_IMAGE_DATA", "IMAGE_ID=" + image_id, "");
                    //if (images == null)
                    //{
                    //    _errMsg = _conn.errorMessage;
                    //    return false;
                    //}
                    //if (images.Rows.Count > 0)
                    //{
                    //    foreach (DataRow r in images.Rows)
                    //    {
                    //        r.Delete();

                    //    }
                    //    if (_conn.Update(images))
                    //    {
                    //        _errMsg = _conn.errorMessage;
                    //        return false;
                    //    }
                    //}
                    break;
                default:
                    try
                    {
                        FileInfo fi = new FileInfo(imageSpace + @"/" + managed_file);
                        if (!fi.Exists)
                        {
                            _errMsg = "Can't find file '" + fi.FullName + "'";
                            return false;
                        }
                        fi.Delete();
                    }
                    catch (Exception ex)
                    {
                        _errMsg = ex.Message;
                        return false;
                    }
                    break;
            }
            return true;
        }

        async public Task<int> GetImageDatasetItemID(string image_path)
        {
            if (_conn == null)
            {
                return -1;
            }

            object ID = await _conn.QuerySingleField($"SELECT {DataProvider.ToDbFieldName(_conn.dbTypeString, "FDB_OID")} FROM {DataProvider.ToDbTableName(_conn.dbTypeString, "FC_" + _dsname + "_IMAGE_POLYGONS")} WHERE {DataProvider.ToDbFieldName(_conn.dbTypeString, "PATH")}='" + image_path + "'", "FDB_OID");
            if (ID == null)
            {
                return -1;
            }

            return Convert.ToInt32(ID);
        }
        #endregion

        #region Import

        private string[] _formats = "jpg|png|tif|tiff|sid|jp2".Split('|');
        private int _levels = 4;
        private bool _managed = false;

        async public Task<bool> Import(string path, Dictionary<string, Guid> providers)
        {
            //_cancelTracker.Reset();
            if (!_cancelTracker.Continue)
            {
                _errMsg = "Canceled by user...";
                return false;
            }

            if (!(_fdb is AccessFDB))
            {
                _errMsg = "Database in not a FeatureDatabase...";
                return false;
            }

            AccessFDB fdb = (AccessFDB)_fdb;

            if (ReportAction != null)
            {
                ReportAction(this, "Open Raster Polygon Class...");
            }

            IFeatureClass rasterFC = await fdb.GetFeatureclass(_dsname, _dsname + "_IMAGE_POLYGONS");
            if (rasterFC == null)
            {
                if (rasterFC == null)
                {
                    Console.WriteLine("\n\nERROR: Open Featureclass - Can't init featureclass " + _dsname + "_IMAGE_POLYGONS");
                    return false;
                }
            }

            DirectoryInfo di = new DirectoryInfo(path);
            FileInfo fi = new FileInfo(path);
            if (di.Exists)
            {
                await SearchImages(fdb, rasterFC, di, providers);
                //if (_recalculateExtent) fdb.CalculateSpatialIndex(rasterFC, 100);
            }
            else if (fi.Exists)
            {
                if (!await InsertImage(fdb, rasterFC, fi, providers))
                {
                    return false;
                }
                //if (_recalculateExtent) fdb.CalculateSpatialIndex(rasterFC, 100);
            }

            if (!_cancelTracker.Continue)
            {
                _errMsg = "Canceled by user...";
                return false;
            }

            return true;
        }

        async private Task SearchImages(IFeatureUpdater fdb, IFeatureClass rasterFC, DirectoryInfo di, Dictionary<string, Guid> providers)
        {
            if (!_cancelTracker.Continue)
            {
                return;
            }

            if (ReportAction != null)
            {
                ReportAction(this, "Scan Directory: " + di.FullName);
            }

            foreach (string filter in _formats)
            {
                int count = 0;
                foreach (FileInfo fi in di.GetFiles("*." + filter))
                {
                    if (_cancelTracker != null && !_cancelTracker.Continue)
                    {
                        return;
                    }

                    if (await InsertImage(fdb, rasterFC, fi, providers))
                    {
                        count++;
                    }

                    if (!_cancelTracker.Continue)
                    {
                        return;
                    }
                }
            }
            foreach (DirectoryInfo sub in di.GetDirectories())
            {
                await SearchImages(fdb, rasterFC, sub, providers);
            }
        }

        async private Task<bool> InsertImage(IFeatureUpdater fdb, IFeatureClass rasterFC, FileInfo fi, Dictionary<string, Guid> providers)
        {
            if (!_cancelTracker.Continue)
            {
                _errMsg = "Canceled by user...";
                return false;
            }

            if (ReportAction != null)
            {
                ReportAction(this, "Insert Image: " + fi.FullName);
            }

            if (ReportProgress != null)
            {
                ReportProgress(this, 1);
            }

            #region RasterFileDataset bestimmen

            List<IRasterFileDataset> rFileDatasets = new List<IRasterFileDataset>();
            IRasterFileDataset rFileDataset = null;
            IRasterLayer rasterLayer = null;
            PlugInManager compMan = new PlugInManager();

            foreach (var dsType in compMan.GetPlugins(Plugins.Type.IDataset))
            {
                IRasterFileDataset rds = compMan.CreateInstance<IDataset>(dsType) as IRasterFileDataset;
                if (rds == null)
                {
                    continue;
                }

                if (rds.SupportsFormat(fi.Extension) < 0)
                {
                    continue;
                }

                if (providers != null && providers.ContainsKey(fi.Extension.ToLower()))
                {
                    if (!providers[fi.Extension.ToLower()].Equals(PlugInManager.PlugInID(rds)))
                    {
                        continue;
                    }
                }
                rFileDatasets.Add(rds);
            }
            if (rFileDatasets.Count == 0)
            {
                _errMsg = "No Rasterfile Provider for " + fi.Extension;
                return false;
            }

            // RasterFileDataset nach priorit�t sortieren
            rFileDatasets.Sort(new RasterFileDatasetComparer(fi.Extension));

            // RasterFileDataset suchen, mit dem sich Bild �ffnen l��t
            foreach (IRasterFileDataset rfd in rFileDatasets)
            {
                rfd.AddRasterFile(fi.FullName);

                if ((await rfd.Elements()).Count == 0)
                {
                    //Console.WriteLine(_errMsg = fi.FullName + " is not a valid Raster File...");
                    continue;
                }
                IDatasetElement element = (await rfd.Elements())[0];
                if (!(element is IRasterLayer))
                {
                    //Console.WriteLine(_errMsg = fi.FullName + " is not a valid Raster Layer...");
                    rFileDataset.Dispose();
                    continue;
                }

                // gefunden...
                rasterLayer = (IRasterLayer)element;
                rFileDataset = rfd;
                break;
            }
            if (rasterLayer == null || rFileDataset == null)
            {
                Console.WriteLine(_errMsg = fi.FullName + " is not a valid Raster Layer...");
                return false;
            }

            #endregion

            FileInfo fiWorld = null;
            double cellX = 1;
            double cellY = 1;

            IRasterFile rasterFile = null;
            if (rasterLayer is IRasterFile)
            {
                rasterFile = (IRasterFile)rasterLayer;
            }
            if (rasterLayer.RasterClass != null)
            {
                if (rasterLayer.RasterClass is IRasterFile &&
                    ((IRasterFile)rasterLayer.RasterClass).WorldFile != null)
                {
                    rasterFile = (IRasterFile)rasterLayer.RasterClass;
                }
                else
                {
                    IRasterClass c = rasterLayer.RasterClass;
                    cellX = Math.Sqrt(c.dx1 * c.dx1 + c.dx2 * c.dx2);
                    cellY = Math.Sqrt(c.dy1 * c.dy1 + c.dy2 * c.dy2);
                }
            }
            if (rasterFile != null)
            {
                try
                {
                    IRasterWorldFile world = rasterFile.WorldFile;
                    if (world != null)
                    {
                        if (!world.isGeoReferenced)
                        {
                            if (handleNonGeorefAsError)
                            {
                                _errMsg = "Can't add non georeferenced images: " + fi.FullName;
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }

                        cellX = Math.Sqrt(world.dx_X * world.dx_X + world.dx_Y * world.dx_Y);
                        cellY = Math.Sqrt(world.dy_X * world.dy_X + world.dy_Y * world.dy_Y);

                        fiWorld = new FileInfo(rasterFile.WorldFile.Filename);
                        if (!fiWorld.Exists)
                        {
                            fiWorld = null;
                        }
                    }
                }
                catch
                {
                    fiWorld = null;
                }
            }

            #region Check if already Exits
            //
            // Suchen, ob Image mit gleichen Pfad schon vorhanden ist, wenn ja
            // nur weitermachen, wenn sich das �nderungsdatum unterscheidet...
            //

            QueryFilter filter = new QueryFilter();
            filter.AddField("FDB_OID");
            filter.AddField("PATH");
            filter.AddField("LAST_MODIFIED");
            filter.AddField("PATH2");
            filter.AddField("LAST_MODIFIED2");

            if (_fdb is AccessFDB)
            {
                filter.WhereClause = ((AccessFDB)_fdb).DbColName("PATH") + "='" + ((AccessFDB)_fdb).EscapeQueryValue(fi.FullName) + "'";
            }
            else
            {
                filter.WhereClause = "PATH='" + fi.FullName.Replace(@"\", @"\\") + "'";
            }

            int deleteOID = -1;
            using (IFeatureCursor cursor = await rasterFC.GetFeatures(filter))
            {
                IFeature existingFeature = await cursor.NextFeature();
                if (existingFeature != null)
                {
                    DateTime dt1 = (DateTime)existingFeature["LAST_MODIFIED"];

                    if (!fi.LastWriteTimeUtc.IsEqual2(dt1, 1))
                    {
                        deleteOID = existingFeature.OID;
                    }
                    else if (fiWorld != null &&
                        existingFeature["PATH2"] != System.DBNull.Value &&
                        existingFeature["PATH2"].ToString() != String.Empty)
                    {
                        DateTime dt2 = (DateTime)existingFeature["LAST_MODIFIED2"];
                        if (existingFeature["PATH2"].ToString().ToLower() != fiWorld.FullName.ToLower() ||
                            Math.Abs((dt2 - fiWorld.LastWriteTimeUtc).TotalSeconds) > 1.0)
                        {
                            deleteOID = existingFeature.OID;
                        }
                    }

                    if (deleteOID == -1)
                    {
                        Console.Write(".");
                        //Console.WriteLine(fi.FullName + " already exists...");
                        return true;
                    }
                }
            }
            if (deleteOID != -1)
            {
                if (!await fdb.Delete(rasterFC, deleteOID))
                {
                    Console.WriteLine(_errMsg = "Can't delete old record " + fi.FullName + "\n" + fdb.LastErrorMessage);
                    return false;
                }
            }
            //
            ///////////////////////////////////////////////////////////////////
            //
            #endregion

            Feature feature = new Feature();
            feature.Shape = rasterLayer.RasterClass.Polygon;
            feature.Fields.Add(new FieldValue("PATH", fi.FullName));
            feature.Fields.Add(new FieldValue("LAST_MODIFIED", fi.LastWriteTimeUtc));
            if (fiWorld != null)
            {
                feature.Fields.Add(new FieldValue("PATH2", fiWorld.FullName));
                feature.Fields.Add(new FieldValue("LAST_MODIFIED2", fiWorld.LastWriteTimeUtc));
            }
            else
            {
                feature.Fields.Add(new FieldValue("PATH2", ""));
            }
            feature.Fields.Add(new FieldValue("RF_PROVIDER", PlugInManager.PlugInID(rFileDataset).ToString()));
            feature.Fields.Add(new FieldValue("MANAGED", _managed && (rasterLayer is IRasterFileBitmap)));
            feature.Fields.Add(new FieldValue("FORMAT", fi.Extension.Replace(".", "")));
            feature.Fields.Add(new FieldValue("CELLX", cellX));
            feature.Fields.Add(new FieldValue("CELLY", cellY));
            feature.Fields.Add(new FieldValue("LEVELS", (_managed) ? Math.Max(_levels, 1) : 0));

            if (!await fdb.Insert(rasterFC, feature))
            {
                Console.WriteLine("\nERROR@" + fi.FullName + ": " + fdb.LastErrorMessage);
            }
            else
            {
                //if (_managed && (rasterLayer is IBitmap) && (fdb is SqlFDB))
                //{
                //    QueryFilter qfilter = new QueryFilter();
                //    qfilter.SubFields = "FDB_OID";
                //    if (_fdb is AccessFDB)
                //    {
                //        filter.WhereClause = ((AccessFDB)_fdb).DbColName("PATH") + "='" + fi.FullName + "'";
                //    }
                //    else
                //    {
                //        qfilter.WhereClause = "PATH='" + fi.FullName + "'";
                //    }

                //    IFeatureCursor cursor = await ((SqlFDB)fdb).Query(rasterFC, qfilter);
                //    if (cursor != null)
                //    {
                //        IFeature feat = await cursor.NextFeature();
                //        if (feat != null)
                //        {
                //            await InsertImageDatasetBitmap(feat.OID, rasterLayer, _levels, format);
                //        }
                //        cursor.Dispose();
                //    }
                //}

                Console.WriteLine(">" + fi.FullName + " added...");
            }

            rFileDataset.Dispose();
            return true;
        }
        #endregion

        #region Remove Unexisting

        async public Task<bool> RemoveUnexisting()
        {
            if (!(_fdb is AccessFDB))
            {
                return false;
            }

            IFeatureClass rasterFC = await ((AccessFDB)_fdb).GetFeatureclass(_dsname, _dsname + "_IMAGE_POLYGONS");
            if (rasterFC == null)
            {
                if (rasterFC == null)
                {
                    Console.WriteLine("\n\nERROR: Open Featureclass - Can't init featureclass " + _dsname + "_IMAGE_POLYGONS");
                    return false;
                }
            }

            if (ReportAction != null)
            {
                ReportAction(this, "Remove unexisting");
            }

            if (ReportProgress != null)
            {
                ReportProgress(this, 1);
            }

            QueryFilter filter = new QueryFilter();
            filter.AddField("FDB_OID");
            filter.AddField("PATH");
            filter.AddField("LAST_MODIFIED");
            filter.AddField("PATH2");
            filter.AddField("LAST_MODIFIED2");

            List<int> Oids = new List<int>();
            using (IFeatureCursor cursor = await rasterFC.GetFeatures(filter))
            {
                IFeature feature;
                while ((feature = await cursor.NextFeature()) != null)
                {
                    string path = (string)feature["PATH"];
                    string path2 = (string)feature["PATH2"];
                    try
                    {
                        if (!String.IsNullOrEmpty(path))
                        {
                            FileInfo fi = new FileInfo(path);
                            if (!fi.Exists)
                            {
                                ReportAction(this, $"Remove {fi.Name}");
                                Oids.Add(feature.OID);
                                continue;
                            }
                        }
                        if (!String.IsNullOrEmpty(path2))
                        {
                            FileInfo fi = new FileInfo(path2);
                            if (!fi.Exists)
                            {
                                ReportAction(this, $"Remove {fi.Name}");
                                Oids.Add(feature.OID);
                                continue;
                            }
                        }
                        ReportAction(this, ".");
                    }
                    catch (Exception ex)
                    {
                        ReportAction(this, _errMsg = "Exception: " + ex.Message);
                    }
                }

                foreach (int oid in Oids)
                {
                    if (!await ((AccessFDB)_fdb).Delete(rasterFC, oid))
                    {
                        ReportAction(this, _errMsg = "Can't delete record OID=" + oid);
                        return false;
                    }
                }

                return true;
            }
        }

        #endregion

        #region Check if Exists

        async public Task<bool> CheckIfExists()
        {
            if (!(_fdb is AccessFDB))
            {
                return false;
            }

            IFeatureClass rasterFC = await ((AccessFDB)_fdb).GetFeatureclass(_dsname, _dsname + "_IMAGE_POLYGONS");
            if (rasterFC == null)
            {
                if (rasterFC == null)
                {
                    Console.WriteLine("\n\nERROR: Open Featureclass - Can't init featureclass " + _dsname + "_IMAGE_POLYGONS");
                    return false;
                }
            }

            if (ReportAction != null)
            {
                ReportAction(this, "Find not existing files");
            }

            if (ReportProgress != null)
            {
                ReportProgress(this, 1);
            }

            QueryFilter filter = new QueryFilter();
            filter.AddField("FDB_OID");
            filter.AddField("PATH");
            int count = 0, countNotExisting = 0;

            using (IFeatureCursor cursor = await rasterFC.GetFeatures(filter))
            {
                IFeature feature;
                while ((feature = await cursor.NextFeature()) != null)
                {
                    count++;

                    if(!_cancelTracker.Continue)
                    {
                        ReportAction(this, $"canceled");
                        break;
                    }

                    string path = (string)feature["PATH"];
                    string path2 = (string)feature["PATH2"];
                    try
                    {
                        if (!String.IsNullOrEmpty(path))
                        {
                            FileInfo fi = new FileInfo(path);
                            if (!fi.Exists)
                            {
                                ReportAction(this, $"file not exists: {fi.FullName}");
                                countNotExisting++;
                                continue;
                            }
                        }

                        ReportAction(this, count % 50 == 0 ? $".{count}{Environment.NewLine}" : ".");
                    }
                    catch (Exception ex)
                    {
                        ReportAction(this, _errMsg = "Exception: " + ex.Message);
                    }
                }

                if (count % 50 != 0)
                {
                    ReportAction(this, $".{count}{Environment.NewLine}");
                }

                ReportAction(this, $"Total not existing files: {countNotExisting}");

                return countNotExisting == 0;
            }
        }

        #endregion

        #region Helper
        private class RasterFileDatasetComparer : IComparer<IRasterFileDataset>
        {
            private string _extension;
            public RasterFileDatasetComparer(string extension)
            {
                _extension = extension;
            }
            #region IComparer<IRasterFileDataset> Member

            public int Compare(IRasterFileDataset x, IRasterFileDataset y)
            {
                int xx = x.SupportsFormat(_extension);
                int yy = y.SupportsFormat(_extension);

                if (xx > yy)
                {
                    return -1;
                }

                if (xx < yy)
                {
                    return 1;
                }

                return 0;
            }

            #endregion
        }
        #endregion

        #region Errorhandling
        public bool handleNonGeorefAsError = true;
        #endregion
    }
}
