using gView.Framework.Core.Data;
using gView.Framework.Core.FDB;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace gView.Framework.Offline
{
    public interface IFeatureDatabaseReplication : IFeatureDatabase, IFeatureUpdater, IDatabaseNames
    {
        Task<bool> CreateIfNotExists(string tableName, IFieldCollection fields);
        Task<bool> CreateObjectGuidColumn(string fcName, string fieldname);
        Task<int> GetFeatureClassID(string fcName);
        Task<string> GetFeatureClassName(int fcID);

        bool InsertRow(string table, IRow row, IReplicationTransaction replTrans);
        bool InsertRows(string table, List<IRow> rows, IReplicationTransaction replTrans);
        bool UpdateRow(string table, IRow row, string IDField, IReplicationTransaction replTrans);
        bool DeleteRows(string table, string where, IReplicationTransaction replTrans);

        string DatabaseConnectionString { get; }
        DbProviderFactory ProviderFactory { get; }

        string GuidToSql(Guid guid);
        void ModifyDbParameter(DbParameter parameter);

        bool IsFilebaseDatabase { get; }
    }

    public interface IFeatureDatabaseCloudReplication
    {
        // f�r cloudFDB hier kann zB das AllocateNewObjectGuid() bei Konflikten nicht von der Datenbank ausgef�llt werden, weil
        // sonst nichts mehr in die Differences Table eingef�gt werden kann...
        // siehe Replication.CheckIn() #region Conflict Handling
    }

    public interface IFeatureClassDialog
    {
        void ShowDialog(IFeatureClass fc);
    }

    public interface IReplicationTransaction
    {
        bool IsValid { get; }
        int ExecuteNonQuery(DbCommand command);
        object ExecuteScalar(DbCommand command);
    }
}
