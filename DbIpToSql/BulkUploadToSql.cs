using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DbIpToSql
{
    /// <summary>
    /// Written by Amir of StackOverflow with edits by Shane
    /// https://stackoverflow.com/questions/13722014/insert-2-million-rows-into-sql-server-quickly/40156467#40156467
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class BulkUploadToSql<T>
    {
        public List<T>? InternalStore { get; set; }
        public string? TableName { get; set; }
        public int CommitBatchSize { get; set; } = 1000;
        public string? ConnectionString { get; set; }

        public void Commit()
        {
            if (InternalStore != null && InternalStore.Count > 0)
            {
                DataTable dt;
                int length = InternalStore.Count;
                int numberOfPages = length / CommitBatchSize + (length % CommitBatchSize == 0 ? 0 : 1);
                Span<T> span = CollectionsMarshal.AsSpan(InternalStore);
                for (int pageIndex = 0; pageIndex < numberOfPages; pageIndex++)
                {
                    dt = span.Slice(pageIndex * CommitBatchSize, Math.Min(CommitBatchSize, length - (pageIndex * CommitBatchSize))).ToDataTable();
                    BulkInsert(dt);
                }
            }
        }

        public async Task CommitAsync()
        {
            if (InternalStore != null && InternalStore.Count > 0)
            {
                DataTable dt;
                int length = InternalStore.Count;
                int numberOfPages = length / CommitBatchSize + (length % CommitBatchSize == 0 ? 0 : 1);
                for (int pageIndex = 0; pageIndex < numberOfPages; pageIndex++)
                {
                    dt = InternalStore.GetRange(pageIndex * CommitBatchSize, Math.Min(CommitBatchSize, length - (pageIndex * CommitBatchSize))).ToDataTable();
                    await BulkInsertAsync(dt);
                }
            }
        }

        void BulkInsert(DataTable dt)
        {
            using (SqlConnection sqlConnection = new SqlConnection(ConnectionString))
            {
                // make sure to enable triggers
                // more on triggers in next post
                SqlBulkCopy bulkCopy =
                    new SqlBulkCopy
                    (
                        sqlConnection,
                        SqlBulkCopyOptions.TableLock |
                        SqlBulkCopyOptions.FireTriggers |
                        SqlBulkCopyOptions.UseInternalTransaction,
                        null
                    );

                //ADD COLUMN MAPPING
                foreach (DataColumn col in dt.Columns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }

                // set the destination table name
                bulkCopy.DestinationTableName = TableName;
                sqlConnection.Open();

                try
                {
                    // write the data in the "dataTable"
                    bulkCopy.WriteToServer(dt);
                }
                catch (SqlException ex)
                {
                    if (TryParseInvalidColumnLengthException(bulkCopy, ex, out string? columnName, out int? columnLength))
                    {
                        throw new FormatException($"Column: \"{columnName}\" contains data with a length greater than: {columnLength}");
                    }

                    throw;
                }

                sqlConnection.Close();
            }
            // reset
            //this.dataTable.Clear();
        }

        async Task BulkInsertAsync(DataTable dt)
        {
            using (SqlConnection sqlConnection = new SqlConnection(ConnectionString))
            {
                // make sure to enable triggers
                // more on triggers in next post
                SqlBulkCopy bulkCopy =
                    new SqlBulkCopy
                    (
                        sqlConnection,
                        SqlBulkCopyOptions.TableLock |
                        SqlBulkCopyOptions.FireTriggers |
                        SqlBulkCopyOptions.UseInternalTransaction,
                        null
                    );

                //ADD COLUMN MAPPING
                foreach (DataColumn col in dt.Columns)
                {
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                }

                // set the destination table name
                bulkCopy.DestinationTableName = TableName;
                await sqlConnection.OpenAsync();

                try
                {
                    // write the data in the "dataTable"
                    await bulkCopy.WriteToServerAsync(dt);
                }
                catch (SqlException ex)
                {
                    if (TryParseInvalidColumnLengthException(bulkCopy, ex, out string? columnName, out int? columnLength))
                    {
                        throw new FormatException($"Column: \"{columnName}\" contains data with a length greater than: {columnLength}");
                    }

                    throw;
                }

                await sqlConnection.CloseAsync();
            }
            // reset
            //this.dataTable.Clear();
        }

        /// <summary>
        /// <see cref="ConnectionString"/> is ignored when using this function. If the <paramref name="sqlConnection"/> is not open, it will be opened.
        /// </summary>
        /// <param name="sqlConnection">The <see cref="SqlConnection"/> to run the query on.</param>
        /// <returns></returns>
        public void Commit(SqlConnection sqlConnection)
        {
            if (InternalStore != null && InternalStore.Count > 0)
            {
                DataTable dt;
                int numberOfPages = InternalStore.Count / CommitBatchSize + (InternalStore.Count % CommitBatchSize == 0 ? 0 : 1);
                for (int pageIndex = 0; pageIndex < numberOfPages; pageIndex++)
                {
                    dt = InternalStore.Skip(pageIndex * CommitBatchSize).Take(CommitBatchSize).ToDataTable();
                    BulkInsert(dt, sqlConnection);
                }
            }
        }

        /// <summary>
        /// <see cref="ConnectionString"/> is ignored when using this function. If the <paramref name="sqlConnection"/> is not open, it will be opened.
        /// </summary>
        /// <param name="sqlConnection">The <see cref="SqlConnection"/> to run the query on.</param>
        /// <returns></returns>
        public async Task CommitAsync(SqlConnection sqlConnection)
        {
            if (InternalStore != null && InternalStore.Count > 0)
            {
                DataTable dt;
                int numberOfPages = InternalStore.Count / CommitBatchSize + (InternalStore.Count % CommitBatchSize == 0 ? 0 : 1);
                for (int pageIndex = 0; pageIndex < numberOfPages; pageIndex++)
                {
                    dt = InternalStore.Skip(pageIndex * CommitBatchSize).Take(CommitBatchSize).ToDataTable();
                    await BulkInsertAsync(dt, sqlConnection);
                }
            }
        }

        void BulkInsert(DataTable dt, SqlConnection sqlConnection)
        {
            // make sure to enable triggers
            // more on triggers in next post
            SqlBulkCopy bulkCopy =
                new SqlBulkCopy
                (
                    sqlConnection,
                    SqlBulkCopyOptions.TableLock |
                    SqlBulkCopyOptions.FireTriggers |
                    SqlBulkCopyOptions.UseInternalTransaction,
                    null
                );

            //ADD COLUMN MAPPING
            foreach (DataColumn col in dt.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            // set the destination table name
            bulkCopy.DestinationTableName = TableName;

            // Open the connection if it isn't already
            if (sqlConnection.State != ConnectionState.Open)
            {
                sqlConnection.Open();
            }

            try
            {
                // write the data in the "dataTable"
                bulkCopy.WriteToServer(dt);
            }
            catch (SqlException ex)
            {
                if (TryParseInvalidColumnLengthException(bulkCopy, ex, out string? columnName, out int? columnLength))
                {
                    throw new FormatException($"Column: \"{columnName}\" contains data with a length greater than: {columnLength}");
                }

                throw;
            }
        }

        async Task BulkInsertAsync(DataTable dt, SqlConnection sqlConnection)
        {
            // make sure to enable triggers
            // more on triggers in next post
            SqlBulkCopy bulkCopy =
                new SqlBulkCopy
                (
                    sqlConnection,
                    SqlBulkCopyOptions.TableLock |
                    SqlBulkCopyOptions.FireTriggers |
                    SqlBulkCopyOptions.UseInternalTransaction,
                    null
                );

            //ADD COLUMN MAPPING
            foreach (DataColumn col in dt.Columns)
            {
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            }

            // set the destination table name
            bulkCopy.DestinationTableName = TableName;

            // Open the connection if it isn't already
            if (sqlConnection.State != ConnectionState.Open)
            {
                await sqlConnection.OpenAsync();
            }

            try
            {
                // write the data in the "dataTable"
                await bulkCopy.WriteToServerAsync(dt);
            }
            catch (SqlException ex)
            {
                if (TryParseInvalidColumnLengthException(bulkCopy, ex, out string? columnName, out int? columnLength))
                {
                    throw new FormatException($"Column: \"{columnName}\" contains data with a length greater than: {columnLength}");
                }

                throw;
            }
        }

        bool TryParseInvalidColumnLengthException(SqlBulkCopy bulkCopy, SqlException ex, out string? columnName, out int? columnLength)
        {
            columnName = null;
            columnLength = null;

            if (ex.Message.Contains("Received an invalid column length from the bcp client for colid"))
            {
                string pattern = @"\d+";
                Match match = Regex.Match(ex.Message.ToString(), pattern);
                int index = Convert.ToInt32(match.Value) - 1;

                FieldInfo fi = typeof(SqlBulkCopy).GetField("_sortedColumnMappings", BindingFlags.NonPublic | BindingFlags.Instance)!;
                object sortedColumns = fi.GetValue(bulkCopy)!;
                object[] items = (object[])sortedColumns.GetType().GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(sortedColumns)!;

                FieldInfo itemdata = items[index].GetType().GetField("_metadata", BindingFlags.NonPublic | BindingFlags.Instance)!;
                object metadata = itemdata.GetValue(items[index])!;

                object column = metadata.GetType().GetField("column", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(metadata)!;
                object length = metadata.GetType().GetField("length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(metadata)!;

                columnName = column as string;
                columnLength = length as int?;

                return true;
            }

            return false;
        }
    }

    public static class BulkUploadToSqlHelper
    {
        private static ConcurrentDictionary<Type, List<(PropertyDescriptor prop, string remoteColumnName)>> _propertiesCache =
            new ConcurrentDictionary<Type, List<(PropertyDescriptor prop, string remoteColumnName)>>();

        public static DataTable ToDataTable<T>(this IEnumerable<T> data)
        {
            DataTable table = new DataTable();

            if (_propertiesCache.TryGetValue(typeof(T), out List<(PropertyDescriptor prop, string remoteColumnName)>? propertiesToKeep))
            {
                foreach ((PropertyDescriptor prop, string remoteColumnName) in propertiesToKeep)
                {
                    table.Columns.Add(remoteColumnName ?? prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }
            }
            else
            {
                PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));

                List<(PropertyDescriptor prop, string remoteColumnName)> newPropertiesList = new List<(PropertyDescriptor prop, string remoteColummnName)>();

                foreach (PropertyDescriptor prop in properties)
                {
                    // Get BulkUploadToSql settings for property
                    BulkUploadToSqlSettingsAttribute? bulkUploadToSqlSettingsAttribute = prop.Attributes.OfType<BulkUploadToSqlSettingsAttribute>().FirstOrDefault();

                    string? remoteColumName = null;

                    if (bulkUploadToSqlSettingsAttribute != null)
                    {
                        // Skip properties that should be ignored
                        if (bulkUploadToSqlSettingsAttribute.ColumnIgnored)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(bulkUploadToSqlSettingsAttribute.RemoteColumnName))
                        {
                            remoteColumName = bulkUploadToSqlSettingsAttribute.RemoteColumnName;
                        }
                    }

                    newPropertiesList.Add((prop, remoteColumName ?? prop.Name));
                    table.Columns.Add(remoteColumName ?? prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }

                if (newPropertiesList.Count == 0)
                {
                    throw new Exception($"Type {typeof(T)} does not contain any properties to be uploaded.");
                }

                _propertiesCache.TryAdd(typeof(T), newPropertiesList);
                propertiesToKeep = newPropertiesList;
            }

            foreach (T item in data)
            {
                DataRow row = table.NewRow();

                foreach ((PropertyDescriptor prop, string remoteColumnName) in propertiesToKeep)
                {
                    row[remoteColumnName] = prop.GetValue(item) ?? DBNull.Value;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        public static DataTable ToDataTable<T>(this Span<T> data)
        {
            DataTable table = new DataTable();

            if (_propertiesCache.TryGetValue(typeof(T), out List<(PropertyDescriptor prop, string remoteColumnName)>? propertiesToKeep))
            {
                foreach ((PropertyDescriptor prop, string? remoteColumnName) in propertiesToKeep)
                {
                    table.Columns.Add(remoteColumnName ?? prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }
            }
            else
            {
                PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));

                List<(PropertyDescriptor prop, string remoteColumnName)> newPropertiesList = new List<(PropertyDescriptor prop, string remoteColummnName)>();

                foreach (PropertyDescriptor prop in properties)
                {
                    // Get BulkUploadToSql settings for property
                    BulkUploadToSqlSettingsAttribute? bulkUploadToSqlSettingsAttribute = prop.Attributes.OfType<BulkUploadToSqlSettingsAttribute>().FirstOrDefault();

                    string? remoteColumName = null;

                    if (bulkUploadToSqlSettingsAttribute != null)
                    {
                        // Skip properties that should be ignored
                        if (bulkUploadToSqlSettingsAttribute.ColumnIgnored)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(bulkUploadToSqlSettingsAttribute.RemoteColumnName))
                        {
                            remoteColumName = bulkUploadToSqlSettingsAttribute.RemoteColumnName;
                        }
                    }

                    newPropertiesList.Add((prop, remoteColumName ?? prop.Name));
                    table.Columns.Add(remoteColumName ?? prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                }

                if (newPropertiesList.Count == 0)
                {
                    throw new Exception($"Type {typeof(T)} does not contain any properties to be uploaded.");
                }

                _propertiesCache.TryAdd(typeof(T), newPropertiesList);
                propertiesToKeep = newPropertiesList;
            }

            foreach (T item in data)
            {
                DataRow row = table.NewRow();

                foreach ((PropertyDescriptor prop, string? remoteColumnName) in propertiesToKeep)
                {
                    row[remoteColumnName] = prop.GetValue(item) ?? DBNull.Value;
                }

                table.Rows.Add(row);
            }

            return table;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    internal sealed class BulkUploadToSqlSettingsAttribute : Attribute
    {
        private readonly string? remoteColumnName;
        private readonly bool columnIgnored;

        // Constructor
        public BulkUploadToSqlSettingsAttribute(string? RemoteColumnName = null, bool ColumnIgnored = false)
        {
            remoteColumnName = RemoteColumnName;
            columnIgnored = ColumnIgnored;
        }

        // property to get RemoteColumnName
        public string? RemoteColumnName
        {
            get { return remoteColumnName; }
        }

        // property to get ColumnIgnored
        public bool ColumnIgnored
        {
            get { return columnIgnored; }
        }
    }
}
