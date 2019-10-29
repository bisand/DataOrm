using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using MySql.Data.MySqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using DataOrm.DataAccess.Common.Attributes;
using DataOrm.DataAccess.Common.Interfaces;
using DataOrm.DataAccess.Common.Models;
using DataOrm.DataAccess.Common.Threading;
using System.Threading.Tasks;

namespace DataOrm.DataAccess.Communication.Implementations
{
    public class MySqlServer : DataOrmServer, IAsyncDataAccess
    {
        private const int MaxBatchSIze = 0x10000;
        //private static readonly ILogger Logger;
        private readonly MySqlConnection _connection;
        private string _connectionString;
        private bool _disposed;

        #region IAsyncDataAccess Members

        public MySqlServer()
        {
        }

        public MySqlServer(string connectionString) : this()
        {
            _connectionString = connectionString;
            _connection = new MySqlConnection(connectionString);
            _connection.Open();
        }

        public MySqlServer(MySqlConnection connection) : this()
        {
            _connection = connection;
            if (_connection == null || _connection.GetType() != typeof(MySqlConnection))
            {
                throw new ArgumentException("The connection needs to be of type MySqlConnection");
            }
            _connection.Open();
            _connectionString = _connection.ConnectionString;
        }

        public int? ConnectionTimeout { get; set; }

        public string ConnectionString
        {
            get { return _connectionString; }
            set { _connectionString = value; }
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public List<T> Query<T>(string query) where T : new()
        {
            var asyncResult = BeginQuery<T>(query, null, null);
            if (asyncResult.IsCompleted || asyncResult.AsyncWaitHandle.WaitOne(300000))
            {
                return EndQuery<T>(asyncResult);
            }
            return new List<T>();
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="callback"></param>
        /// <param name="state"></param>
        /// <param name="commandType"></param>
        /// <param name="behaviour"></param>
        /// <returns></returns>
        public IAsyncResult BeginQuery<T>(string query, AsyncCallback callback, object state, CommandType commandType = CommandType.Text, CommandBehavior behaviour = CommandBehavior.Default) where T : new()
        {
            var asyncResult = new SqlAsyncResult<T>(callback, state);
            try
            {
                asyncResult.CreateCommand = CreateCommand;
                asyncResult.ExecuteReader = ExecuteReader;
                asyncResult.LoadWithOptions = LoadWithOptions.ToDictionary(x => x.Key, y => y.Value);
                asyncResult.Parameters = Parameters;
                asyncResult.Behaviour = behaviour;
                asyncResult.Result = new List<T>();
                asyncResult.Command = CreateCommand(query, null, commandType, Parameters);
                if (asyncResult.Command == null)
                    throw new NullReferenceException("IDbCommand is null. Now work will be done.");
                asyncResult.InternalAsyncResult = asyncResult.ExecuteReader.BeginInvoke(asyncResult.Command, asyncResult.Behaviour, ExecuteReaderCallback<T>, asyncResult);
            }
            catch (Exception ex)
            {
                //Logger.LogError(e);
                Console.WriteLine(ex);
                asyncResult.SetCompleted();
            }
            return asyncResult;
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ar"></param>
        /// <returns></returns>
        public List<T> EndQuery<T>(IAsyncResult ar) where T : new()
        {
            if (!(ar is SqlAsyncResult<T>))
                return new List<T>();

            var asyncResult = (ar as SqlAsyncResult<T>);
            if (asyncResult.Command != null)
            {
                asyncResult.Command.Connection.Close();
                asyncResult.Command.Dispose();
                asyncResult.Command = null;
            }
            return asyncResult.Result;
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        public void LoadWith<T>(Expression<Func<T, Object>> expression)
        {
            var option = new LoadWithOption();
            var member = (MemberExpression)expression.Body;
            option.SourceType = typeof(T);
            option.PropertyName = member.Member.Name;
            option.PropertyType = member.Type;
            option.DeclaringType = member.Member.DeclaringType;
            var attribute = member.Member.GetCustomAttributes(typeof(NavigationPropertyAttribute), false).FirstOrDefault() as NavigationPropertyAttribute;
            if (attribute == null)
                throw new ApplicationException("The LoadWith property must be decorated with the NavigationPropertyAttribute.");

            // Singularize names. They will be pluralized later when querying the database.
            if (option.PropertyType.IsGenericType && option.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
            {
                option.EntityName = string.IsNullOrWhiteSpace(attribute.Entity) ? Singularize(option.PropertyName) : attribute.Entity;
                option.ForeignKey = string.IsNullOrWhiteSpace(attribute.ForeignKey) ? Singularize(option.PropertyName) + "No" : attribute.ForeignKey;
            }
            else
            {
                option.EntityName = string.IsNullOrWhiteSpace(attribute.Entity) ? option.PropertyName : attribute.Entity;
                option.ForeignKey = string.IsNullOrWhiteSpace(attribute.ForeignKey) ? option.PropertyName + "No" : attribute.ForeignKey;
            }
            option.LocalKey = string.IsNullOrWhiteSpace(attribute.LocalKey) ? option.ForeignKey : attribute.LocalKey;
            LoadWithOptions.Add(option, null);
        }

        /// <summary>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="dbType"></param>
        public void AddParameter(string name, object value, DbType dbType)
        {
            Parameters.Add(new MySqlParameter(name, value));
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="ar"></param>
        private void ExecuteReaderCallback<T>(IAsyncResult ar) where T : new()
        {
            if (ar == null || !(ar.AsyncState is SqlAsyncResult<T>))
                return;

            var asyncResult = (ar.AsyncState as SqlAsyncResult<T>);
            try
            {
                using (var dataReader = asyncResult.ExecuteReader.EndInvoke(asyncResult.InternalAsyncResult))
                {
                    if (dataReader == null)
                    {
                        asyncResult.SetCompleted();
                        if (asyncResult.MainAsyncResult != null)
                            asyncResult.MainAsyncResult.SetCompleted();
                        return;
                    }
                    if (asyncResult.MainAsyncResult == null)
                    {
                        asyncResult.Result = ReadData<T>(dataReader);
                        if (asyncResult.Result == null || !asyncResult.Result.Any())
                        {
                            asyncResult.SetCompleted();
                            if (asyncResult.MainAsyncResult != null)
                                asyncResult.MainAsyncResult.SetCompleted();
                            return;
                        }
                        if (asyncResult.LoadWithOptions != null && asyncResult.LoadWithOptions.Any())
                            FetchLoadWithData(asyncResult, typeof (T), asyncResult.Result.Cast<object>().ToList());
                        else
                        {
                            asyncResult.SetCompleted();
                            if (asyncResult.MainAsyncResult != null)
                                asyncResult.MainAsyncResult.SetCompleted();
                        }
                    }
                    else
                    {
                        {
                            var option = asyncResult.MainAsyncResult.LoadWithOptions.Keys.FirstOrDefault(x => x.PropertyName == asyncResult.PropertyName);
                            if (option != null)
                            {
                                var type = option.PropertyType;
                                if (option.PropertyType.IsGenericType && option.PropertyType.GetGenericArguments().Any())
                                    type = option.PropertyType.GetGenericArguments().FirstOrDefault();
                                var data = ReadData(type, dataReader);
                                asyncResult.MainAsyncResult.LoadWithOptions[option] = data;
                                FetchLoadWithData(asyncResult.MainAsyncResult, type, data);
                            }

                            Interlocked.Decrement(ref asyncResult.MainAsyncResult.WorkCounter);
                            if (asyncResult.MainAsyncResult.WorkCounter <= 0)
                            {
                                foreach (var o in asyncResult.MainAsyncResult.LoadWithOptions.ToList())
                                    if (o.Key != null && o.Value != null)
                                        PopulateData(o.Key, asyncResult.MainAsyncResult.Result, o.Value);

                                asyncResult.SetCompleted();
                                if (asyncResult.MainAsyncResult != null)
                                    asyncResult.MainAsyncResult.SetCompleted();
                            }

                            asyncResult.Command.Connection.Close();
                            asyncResult.Command.Dispose();
                            asyncResult.Command = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Logger.LogError(ex);
                asyncResult.Exception = ex;
                asyncResult.SetCompleted();
                if (asyncResult.MainAsyncResult != null)
                    asyncResult.MainAsyncResult.SetCompleted();
            }
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="option"></param>
        /// <param name="targetData"></param>
        /// <param name="data"></param>
        private static void PopulateData(LoadWithOption option, IEnumerable targetData, List<object> data)
        {
            try
            {
                if (data == null)
                    return;
                var destProp = option.DeclaringType.GetProperty(option.PropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                var keyProp = option.DeclaringType.GetProperty(option.ForeignKey, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                foreach (var o in targetData)
                {
                    var oType = o.GetType();
                    var propertyPi = oType.GetProperties().FirstOrDefault(x => x.PropertyType == option.PropertyType || x.PropertyType.GetGenericArguments().Any(y => y == option.PropertyType));
                    if (option.DeclaringType == oType && propertyPi != null)
                    {
                        var key = (keyProp.GetValue(o, null) ?? "").ToString();
                        if (propertyPi.PropertyType.IsGenericType && propertyPi.PropertyType.GetGenericTypeDefinition() == typeof (List<>))
                        {
                            if (destProp.PropertyType.IsGenericType && destProp.PropertyType.GetGenericTypeDefinition() == typeof (List<>))
                                SetValue(destProp, o, data.Where(x => CheckValue(option, x).ToString() == key), propertyPi.PropertyType);
                            else
                                destProp.SetValue(o, data.FirstOrDefault(x => CheckValue(option, x).ToString() == key), null);
                        }
                        else
                            destProp.SetValue(o, data.FirstOrDefault(x => CheckValue(option, x).ToString() == key), null);
                    }
                    else
                    {
                        foreach (var prop in oType.GetProperties().Where(x => x.GetCustomAttributes(typeof (NavigationPropertyAttribute), false).Any()))
                        {
                            var value = prop.GetValue(o, null);
                            if (value == null)
                                continue;
                            var valueList = value as IEnumerable;
                            if (valueList == null)
                                valueList = new List<object> {value};
                            PopulateData(option, valueList, data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Logger.LogError(e);
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="option"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        private static object CheckValue(LoadWithOption option, object x)
        {
            try
            {
                var propertyInfo = x.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase).FirstOrDefault(y => y.Name == option.ForeignKey);
                var result = propertyInfo != null ? propertyInfo.GetValue(x, null) ?? "" : "";
                return result ?? "";
            }
            catch (Exception ex)
            {
                //Logger.LogError(e);
                Console.WriteLine(ex);
                return "";
            }
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mainAsyncResult"></param>
        /// <param name="declaringType"></param>
        /// <param name="dataList"></param>
        private void FetchLoadWithData<T>(SqlAsyncResult<T> mainAsyncResult, Type declaringType, List<object> dataList) where T : new()
        {
            try
            {
                var options = mainAsyncResult.LoadWithOptions.Keys.Where(x => x.DeclaringType == declaringType).ToList();
                foreach (var option in options.Where(option => option.RecursionLevel < 1))
                {
                    Interlocked.Increment(ref option.RecursionLevel);
                    Interlocked.Increment(ref mainAsyncResult.WorkCounter);

                    var stateObject = new SqlAsyncResult<T>(null, null);
                    stateObject.CreateCommand = CreateCommand;
                    stateObject.ExecuteReader = ExecuteReader;
                    stateObject.MainAsyncResult = mainAsyncResult;
                    stateObject.PropertyName = option.PropertyName;

                    var localKey = option.LocalKey.ToLower();
                    var values = new List<string>();
                    var type = option.DeclaringType;
                    var typeName = type.Name;
                    Dictionary<string, PropertyInfo> dataProperties;
                    if ((dataProperties = GetReflections(typeName)) == null)
                    {
                        var propertyInfos = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                        dataProperties = propertyInfos.ToDictionary(x => x.Name, y => y);
                        ReflectedProperties.TryAdd(typeName, dataProperties);
                    }
                    foreach (var data in dataList)
                    {
                        PropertyInfo pi;
                        if (dataProperties.TryGetValue(localKey, out pi))
                        {
                            var o = pi.GetValue(data, null);
                            var value = (o ?? "").ToString();
                            values.Add(value);
                        }
                    }
                    values = values.Distinct().ToList();
                    var query = GetQuery(option, values);
                    var commandType = CommandType.Text;
                    //stateObject.InternalAsyncResult = stateObject.CreateCommand.BeginInvoke(query, option, commandType, null, CreateCommandCallback<T>, stateObject);
                    stateObject.Command = CreateCommand(query, option, commandType);
                    if (stateObject.Command == null)
                        throw new NullReferenceException("IDbCommand is null. Now work will be done.");
                    stateObject.InternalAsyncResult = stateObject.ExecuteReader.BeginInvoke(stateObject.Command, stateObject.Behaviour, ExecuteReaderCallback<T>, stateObject);
                }
            }
            catch (Exception ex)
            {
                //Logger.LogError(e);
                Console.WriteLine(ex);
                throw;
            }
        }


        /// <summary>
        /// </summary>
        /// <param name="info"></param>
        /// <param name="instance"></param>
        /// <param name="values"></param>
        /// <param name="genericArgument"></param>
        private static void SetValue(PropertyInfo info, object instance, IEnumerable<object> values, Type genericArgument)
        {
            try
            {
                if (instance == null)
                    instance = Activator.CreateInstance(info.PropertyType);
                var o = Activator.CreateInstance(info.PropertyType);
                var methodInfo = info.PropertyType.GetMethod("Add");
                foreach (var value in values)
                {
                    methodInfo.Invoke(o, new[] {value});
                }
                info.SetValue(instance, o, null);
            }
            catch (Exception ex)
            {
                //Logger.LogError(e);
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="option"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static string GetQuery(LoadWithOption option, IEnumerable<string> value)
        {
            var tableName = Pluralize(option.EntityName);
            //var fieldNames = GetFieldNames(option.PropertyType);
            var sql = "";
            var enumerable = value as IList<string> ?? value.ToList();
            if (enumerable.Any())
                sql = string.Format("SELECT $(Columns) FROM {0} WHERE {1} IN ('{2}');{3}", tableName, option.ForeignKey, enumerable.Aggregate((f, s) => f + "','" + s), Environment.NewLine);
            else
                sql = string.Format("SELECT $(Columns) FROM {0};{1}", tableName, Environment.NewLine);

            return sql;
        }

        /// <summary>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static IEnumerable<string> GetFieldNames(Type type)
        {
            var result = new List<string>();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (List<>))
                type = type.GetGenericArguments().FirstOrDefault();
            var properties = GetDataProperties(type);
            if (!properties.Keys.Any())
                return result;
            result.AddRange(properties.Keys);
            return result;
        }

        #endregion

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataList"></param>
        /// <param name="entityName">
        ///     Use this attribute if there is a descrepancy between the type name (T) and database table
        ///     name. Name must be in singular format.
        /// </param>
        /// <returns></returns>
        public bool InsertData<T>(List<T> dataList, string entityName = null) where T : new()
        {
            var sql = string.Empty;
            var tType = typeof (T);
            var typeName = tType.Name.ToLower();
            if (entityName == null)
                entityName = typeName;
            var columns = GetTableColumnsFromDatabase(entityName);

            foreach (var data in dataList)
            {
                var fields = string.Empty;
                var values = string.Empty;
                Dictionary<string, PropertyInfo> dataProperties;
                if ((dataProperties = GetReflections(entityName)) == null)
                {
                    var propertyInfos = tType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    dataProperties = propertyInfos.ToDictionary(x => x.Name, y => y);
                    ReflectedProperties.TryAdd(entityName, dataProperties);
                }
                foreach (var pi in dataProperties)
                {
                    string tmpVal;
                    FieldDefinition fd;
                    if (string.IsNullOrWhiteSpace(tmpVal = GetFieldValue(data, pi.Value)) || (fd = columns.FirstOrDefault(x => String.Equals(x.ColumnName, pi.Value.Name, StringComparison.CurrentCultureIgnoreCase))) == null)
                        continue;
                    if (fd.IsAutoIncrement)
                        continue;
                    fields += pi.Value.Name + ",";
                    if (fd.DataType == typeof (string) && tmpVal.Length > fd.ColumnSize)
                        values += tmpVal.Substring(0, fd.ColumnSize - 1) + "',";
                    else if (fd.DataType == typeof (decimal) || fd.DataType == typeof (float) || fd.DataType == typeof (double))
                        values += tmpVal.Replace(",", ".") + ",";
                    else
                        values += tmpVal + ",";
                }
                var tmpSql = string.Format("INSERT INTO {0} ({1}) VALUES ({2});{3}", Pluralize(entityName), fields.TrimEnd(','), values.TrimEnd(','), Environment.NewLine);
                if (sql.Length + tmpSql.Length > MaxBatchSIze)
                {
                    using (var command = CreateCommand(sql))
                    {
                        var transaction = command.Connection.BeginTransaction();
                        command.Transaction = transaction;
                        try
                        {
                            command.ExecuteNonQuery();
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                            transaction.Rollback();
                            return false;
                        }
                    }
                    sql = tmpSql;
                }
                else
                {
                    sql += tmpSql;
                }
            }

            if (!string.IsNullOrWhiteSpace(sql))
            {
                using (var command = CreateCommand(sql))
                {
                    var transaction = command.Connection.BeginTransaction();
                    command.Transaction = transaction;
                    try
                    {
                        command.ExecuteNonQuery();
                        transaction.Commit();
                        //Logger.DebugFormat("Successfully Inserted data.");
                    }
                    catch (Exception ex)
                    {
                        //Logger.LogError(ex);
                        Console.WriteLine(ex);
                        transaction.Rollback();
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataList"></param>
        /// <param name="entityName">
        ///     Use this attribute if there is a descrepancy between the type name (T) and database table
        ///     name. Name must be in singular format.
        /// </param>
        /// <returns></returns>
        public bool UpdateData<T>(List<T> dataList, string entityName = null)
        {
            //Logger.DebugFormat("Updating data of type {0}", typeof (T));
            var sql = string.Empty;
            var tType = typeof (T);
            var typeName = tType.Name;
            if (entityName == null)
                entityName = typeName;
            var columns = GetTableColumnsFromDatabase(entityName);
            foreach (var data in dataList)
            {
                var setters = string.Empty;
                var keyVal = string.Empty;
                Dictionary<string, PropertyInfo> dataProperties;
                if ((dataProperties = GetReflections(entityName)) == null)
                {
                    var propertyInfos = tType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    dataProperties = propertyInfos.ToDictionary(x => x.Name, y => y);
                    ReflectedProperties.TryAdd(entityName, dataProperties);
                }
                foreach (var pi in dataProperties)
                {
                    string tmpVal;
                    FieldDefinition fd;
                    if (!string.IsNullOrWhiteSpace(tmpVal = GetFieldValue(data, pi.Value)) && (fd = columns.FirstOrDefault(x => String.Equals(x.ColumnName, pi.Value.Name, StringComparison.CurrentCultureIgnoreCase))) != null)
                    {
                        if (!fd.IsAutoIncrement)
                        {
                            setters += pi.Value.Name + "=";
                            if (fd.DataType == typeof (string) && tmpVal.Length > fd.ColumnSize)
                                setters += tmpVal.Substring(0, fd.ColumnSize - 1) + "',";
                            else if (fd.DataType == typeof (decimal) || fd.DataType == typeof (float) || fd.DataType == typeof (double))
                                setters += tmpVal.Replace(",", ".") + ",";
                            else
                                setters += tmpVal + ",";
                        }
                        if (fd.IsKey)
                        {
                            keyVal += pi.Value.Name + "=" + tmpVal + " AND";
                        }
                    }
                }
                var tmpSql = string.Format("UPDATE {0} SET {1} WHERE {2};{3}", Pluralize(entityName), setters.TrimEnd(','), keyVal.Remove(keyVal.LastIndexOf(" AND")), Environment.NewLine);
                if (sql.Length + tmpSql.Length > MaxBatchSIze)
                {
                    using (var command = CreateCommand(sql))
                    {
                        var transaction = command.Connection.BeginTransaction();
                        command.Transaction = transaction;
                        try
                        {
                            command.ExecuteNonQuery();
                            transaction.Commit();
                            //Logger.DebugFormat("Successfully updated data.");
                        }
                        catch (Exception ex)
                        {
                            //Logger.LogError(ex);
                            Console.WriteLine(ex);
                            transaction.Rollback();
                            return false;
                        }
                    }
                    sql = tmpSql;
                }
                else
                {
                    sql += tmpSql;
                }
            }
            if (!string.IsNullOrWhiteSpace(sql))
            {
                using (var command = CreateCommand(sql))
                {
                    var transaction = command.Connection.BeginTransaction();
                    command.Transaction = transaction;
                    try
                    {
                        command.ExecuteNonQuery();
                        transaction.Commit();
                        //Logger.DebugFormat("Successfully updated data.");
                    }
                    catch (Exception ex)
                    {
                        //Logger.LogError(ex);
                        Console.WriteLine(ex);
                        transaction.Rollback();
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="option"></param>
        /// <param name="commandType"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public IDbCommand CreateCommand(string sql, LoadWithOption option = null, CommandType commandType = CommandType.Text, List<DbParameter> parameters = null)
        {
            try
            {
                IDbCommand dbCommand;
                if (_connection != null)
                {
                    if (_connection.State != ConnectionState.Open)
                        _connection.Open();
                    dbCommand = new MySqlCommand();
                    dbCommand.Connection = _connection;
                }
                else
                {
                    //dbCommand = ServiceContainer.GetService<IDbCommand>();
                    throw new Exception("Missing connection.");
                }

                if (option != null)
                {
                    var dataColumns = ValidateDataColumns(dbCommand, option);
                    if (dataColumns.Any())
                    {
                        var columnsText = string.Format("[{0}]", dataColumns.Aggregate((f, s) => f + "],[" + s));
                        sql = sql.Replace("$(Columns)", columnsText);
                    }
                    else
                    {
                        sql = sql.Replace("$(Columns)", "*");
                    }
                }

                dbCommand.CommandText = sql;
                dbCommand.CommandType = commandType;
                dbCommand.Parameters.Clear();
                if (parameters != null)
                    foreach (var parameter in parameters)
                        dbCommand.Parameters.Add(parameter);

                if (ConnectionTimeout.HasValue)
                    dbCommand.CommandTimeout = ConnectionTimeout.Value;

                return dbCommand;
            }
            catch (Exception ex)
            {
                //Logger.LogError(ex);
                Console.WriteLine(ex);
                throw;
            }
        }

        private List<string> ValidateDataColumns(IDbCommand command, LoadWithOption option)
        {
            var propertyNames = GetFieldNames(option.PropertyType);
            var databaseColumnNames = GetTableColumnsFromDatabase(option.EntityName, command);
            var columns = propertyNames.Intersect(databaseColumnNames.Select(x => x.ColumnName.ToLower())).ToList();
            return columns;
        }

        /// <summary>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="behavior"></param>
        /// <returns></returns>
        private static IDataReader ExecuteReader(IDbCommand command, CommandBehavior behavior)
        {
            try
            {
                var reader = command.ExecuteReader(behavior);
                return reader;
            }
            catch (Exception ex)
            {
                //Logger.LogError(ex);
                Console.WriteLine(ex);
                throw;
            }
        }

        /// <summary>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private IList<FieldDefinition> GetTableColumnsFromDatabase<T>()
        {
            var tType = typeof (T);
            return GetTableColumnsFromDatabase(tType.Name);
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        private IList<FieldDefinition> GetTableColumnsFromDatabase(string entityName, IDbCommand command = null)
        {
            List<FieldDefinition> result;
            if (TableColumns.TryGetValue(entityName, out result))
            {
                if (result != null && result.Any())
                    return result;
            }

            result = new List<FieldDefinition>();
            var query = string.Format("SELECT * FROM {0} LIMIT 0", Pluralize(entityName));
            if (command != null)
            {
                command.CommandText = query;
            }
            else
            {
                //var MySql = ServiceContainer.GetService<IAsyncDataAccess>();
                command = CreateCommand(query);
            }
            try
            {
                using (var reader = command.ExecuteReader(CommandBehavior.KeyInfo))
                {
                    var schemaTable = reader.GetSchemaTable();

                    if (schemaTable != null)
                    {
                        foreach (DataRow row in schemaTable.Rows)
                        {
                            var fieldDefinition = new FieldDefinition();
                            var t = fieldDefinition.GetType();
                            foreach (DataColumn column in schemaTable.Columns)
                            {
                                var propertyInfo = t.GetProperty(column.ColumnName,
                                    BindingFlags.Instance |
                                    BindingFlags.Public |
                                    BindingFlags.IgnoreCase);
                                propertyInfo.SetValue(fieldDefinition, row[column] != DBNull.Value ? row[column] : null, null);
                            }
                            result.Add(fieldDefinition);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Logger.LogError(ex);
                Console.WriteLine(ex);
                throw;
            }

            //TableColumns.TryAdd(entityName, result);
            TableColumns[entityName] = result;
            return result;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if(!_disposed)
            {
                // If disposing equals true, dispose all managed
                // and unmanaged resources.
                if(disposing)
                {
                    // Dispose managed resources.
                    //component.Dispose();
                    if (_connection != null)
                    {
                        _connection.Close();
                        _connection.Dispose();
                    }
                }

                // Note disposing has been done.
                _disposed = true;

            }
        }

        ~MySqlServer()
        {
            Dispose(false);
        }
    }
}