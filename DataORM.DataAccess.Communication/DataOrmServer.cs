using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DataOrm.DataAccess.Common.Attributes;
using DataOrm.DataAccess.Common.Interfaces;
using DataOrm.DataAccess.Common.Models;
using DataOrm.DataAccess.Common.Threading;
using DataOrm.DataAccess.Communication.Implementations;
using System.Collections;

namespace DataOrm.DataAccess.Communication
{
    public abstract class DataOrmServer : IDisposable
    {
        protected static readonly ConcurrentDictionary<string, Dictionary<string, PropertyInfo>> ReflectedProperties;
        protected static readonly ConcurrentDictionary<string, List<FieldDefinition>> TableColumns;
        protected readonly Dictionary<LoadWithOption, List<object>> LoadWithOptions;

        static DataOrmServer()
        {
            ReflectedProperties = new ConcurrentDictionary<string, Dictionary<string, PropertyInfo>>();
            TableColumns = new ConcurrentDictionary<string, List<FieldDefinition>>();
        }

        protected DataOrmServer()
        {
            DateTimeFormats = new[] { "yyyyMMdd", "yyyy-MM-dd", "dd.MM.yyyy", "yyyyMMddHHmmss", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss" };
            Parameters = new List<DbParameter>();
            LoadWithOptions = new Dictionary<LoadWithOption, List<object>>();
        }

        /// <summary>
        ///     Format used when parsing and settings values of type DateTime.
        /// </summary>
        public string[] DateTimeFormats { get; set; }

        public List<DbParameter> Parameters { get; set; }
        protected abstract int MaxBatchSIze { get; }
        protected abstract string InsertStatement { get; }
        protected abstract string UpdateStatement { get; }

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
            var tType = typeof(T);
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
                    if (fd.DataType == typeof(string) && tmpVal.Length > fd.ColumnSize)
                        values += tmpVal.Substring(0, fd.ColumnSize - 1) + "',";
                    else if (fd.DataType == typeof(decimal) || fd.DataType == typeof(float) || fd.DataType == typeof(double))
                        values += tmpVal.Replace(",", ".") + ",";
                    else
                        values += tmpVal + ",";
                }
                var tmpSql = string.Format(InsertStatement, Pluralize(entityName), fields.TrimEnd(','), values.TrimEnd(','), Environment.NewLine);
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
            var tType = typeof(T);
            var typeName = tType.Name.ToLower();
            if (string.IsNullOrWhiteSpace(entityName))
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
                            if (fd.DataType == typeof(string) && tmpVal.Length > fd.ColumnSize)
                                setters += tmpVal.Substring(0, fd.ColumnSize - 1) + "',";
                            else if (fd.DataType == typeof(decimal) || fd.DataType == typeof(float) || fd.DataType == typeof(double))
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
                var tmpSql = string.Format(UpdateStatement, Pluralize(entityName), setters.TrimEnd(','), keyVal.Remove(keyVal.LastIndexOf(" AND")), Environment.NewLine);
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

        public abstract IDbCommand CreateCommand(string sql, LoadWithOption option = null, CommandType commandType = CommandType.Text, List<DbParameter> parameters = null);
        internal abstract IList<FieldDefinition> GetTableColumnsFromDatabase<T>();
        internal abstract IList<FieldDefinition> GetTableColumnsFromDatabase(string entityName, IDbCommand command = null);

        public static IDataAccess CreateSession(SessionType sessionType, string connectionString)
        {
            switch (sessionType)
            {
                case SessionType.SqlServer:
                    var sqlServer = new MicrosoftSqlServer(connectionString);
                    return sqlServer;
                case SessionType.MySql:
                    var mySql = new MySqlServer(connectionString);
                    return mySql;
            }
            throw new NotImplementedException(string.Format("The session type is not implemented {0}", sessionType));
        }

        public static IDataAccess CreateSession(SessionType sessionType, IDbConnection connection)
        {
            switch (sessionType)
            {
                case SessionType.SqlServer:
                    var sqlServer = new MicrosoftSqlServer(connection as SqlConnection);
                    return sqlServer;
            }
            throw new NotImplementedException(string.Format("The session type is not implemented {0}", sessionType));
        }

        protected List<T> ReadData<T>(IDataReader reader) where T : new()
        {
            var result = new List<T>();

            try
            {
                if (reader != null)
                {
                    var type = typeof(T);
                    var typeName = type.Name.ToLower();
                    var dataProperties = GetDataProperties(type);
                    if (dataProperties == null)
                        return result;
                    while (reader.Read())
                    {
                        var obj = new T();

                        var fieldCount = reader.FieldCount;
                        for (var i = 0; i < fieldCount; i++)
                        {
                            WriteValue(reader, i, dataProperties, obj, type);
                        }
                        result.Add(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex);
                Console.WriteLine(ex);
            }

            return result;
        }

        protected List<object> ReadData(Type type, IDataReader reader)
        {
            var result = new List<object>();

            try
            {
                if (reader != null)
                {
                    var typeName = type.Name.ToLower();
                    var dataProperties = GetDataProperties(type);
                    if (dataProperties == null)
                        return result;
                    while (reader.Read())
                    {
                        var obj = Activator.CreateInstance(type);
                        var fieldCount = reader.FieldCount;
                        for (var i = 0; i < fieldCount; i++)
                        {
                            WriteValue(reader, i, dataProperties, obj, type);
                        }
                        result.Add(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex);
                Console.WriteLine(ex);
            }

            return result;
        }

        protected static Dictionary<string, PropertyInfo> GetDataProperties(Type type)
        {
            var typeName = type.Name;
            Dictionary<string, PropertyInfo> dataProperties;
            if ((dataProperties = GetReflections(typeName)) != null)
                return dataProperties;

            dataProperties = new Dictionary<string, PropertyInfo>();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            foreach (var pi in properties)
            {
                if (pi.GetCustomAttributes(typeof(NavigationPropertyAttribute), false).Any())
                    continue;
                var attribute = pi.GetCustomAttributes(typeof(DatabaseFieldAttribute), false).Cast<DatabaseFieldAttribute>().FirstOrDefault();
                if (attribute != null && !string.IsNullOrWhiteSpace(attribute.FieldName))
                {
                    var fieldName = attribute.FieldName.ToLower();
                    if (dataProperties.ContainsKey(fieldName))
                        dataProperties[fieldName] = pi;
                    else
                        dataProperties.Add(fieldName, pi);
                }
                else
                {
                    dataProperties.Add(pi.Name.ToLower(), pi);
                }
            }
            if (!ReflectedProperties.ContainsKey(typeName))
                ReflectedProperties.TryAdd(typeName, dataProperties);
            return dataProperties;
        }

        private void WriteValue(IDataReader reader, int i, Dictionary<string, PropertyInfo> dataProperties, object obj, Type type)
        {
            var columnName = reader.GetName(i).ToLower();
            if (dataProperties.ContainsKey(columnName))
            {
                var pi = dataProperties[columnName];
                if (pi == null || !pi.CanWrite)
                    return;
                var value = reader[i];
                SetPropertyValue(obj, type, pi, value);
            }
            else
            {
                var pi = type.GetProperty(columnName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi == null || !pi.CanWrite)
                    return;
                var value = reader[i];
                SetPropertyValue(obj, type, pi, value);
            }
        }

        protected static Dictionary<string, PropertyInfo> GetReflections(string name)
        {
            Dictionary<string, PropertyInfo> property;
            return ReflectedProperties.TryGetValue(name, out property) ? property : null;
        }

        protected static List<FieldDefinition> GetColumnNames(string name)
        {
            List<FieldDefinition> columnNames;
            return TableColumns.TryGetValue(name, out columnNames) ? columnNames : null;
        }

        private void SetPropertyValue<T>(T obj, Type type, PropertyInfo pi, object value)
        {
            try
            {
                var oType = (value ?? new object()).GetType();
                var isAssignable = pi.PropertyType.IsAssignableFrom(oType);
                if (isAssignable)
                    SetPropertyValue(pi, obj, value is DBNull ? null : value);
                else
                {
                    var propertyValue = TryGetPropertyValue(value, pi.PropertyType);
                    if (propertyValue != null)
                        SetPropertyValue(pi, obj, propertyValue);
                }
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex);
                Console.WriteLine(ex);
            }
        }

        private void SetPropertyValue(PropertyInfo propertyInfo, object obj, object value)
        {
            propertyInfo.SetValue(obj, value, null);
        }

        private static object TryGetPropertyValue(object value, Type outputType)
        {
            try
            {
                if (value is DBNull || value == null)
                    return null;
                if (outputType == typeof(string))
                    return value.ToString();
                if (outputType == typeof(Guid))
                    return Guid.Parse(value.ToString());

                var result = Convert.ChangeType(value, outputType);
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        protected virtual string GetFieldValue<T>(T client, PropertyInfo pi)
        {
            var value = pi.GetValue(client, null);
            if (IsNullable(pi) && value == null)
                return "null";

            if (GetPropertyType(pi) == typeof(DateTime))
            {
                if (!IsNullable(pi) && value is DateTime &&
                    (value is DateTime ? (DateTime)value : new DateTime()) == DateTime.MinValue)
                    return "cast(0 as datetime)";

                return string.Format("'{0:yyyy-MM-dd HH:mm:ss}'", value);
            }

            if (GetPropertyType(pi) == typeof(bool))
            {
                return ((value is bool && (bool)value)) ? "1" : "0";
            }

            if (GetPropertyType(pi) == typeof(int))
            {
                return ((value is int ? (int)value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof(short))
            {
                return ((value is short ? (short)value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof(long))
            {
                return ((value is long ? (long)value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof(byte))
            {
                return ((value is byte ? (byte)value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof(decimal))
            {
                return ((value is decimal ? (decimal)value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof(float))
            {
                return ((value is float ? (float)value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof(double))
            {
                return ((value is double ? (double)value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof(Guid))
            {
                if (!IsNullable(pi) && value is Guid && (value is Guid ? (Guid)value : new Guid()) == Guid.Empty)
                    return string.Format("'{0}'", Guid.Empty);
                return string.Format("'{0}'", value);
            }

            return string.IsNullOrEmpty(pi.GetValue(client, null) as string)
                ? "null"
                : string.Format("'{0}'", pi.GetValue(client, null).ToString().Replace("'", "''"));
        }

        /// <summary>
        /// </summary>
        /// <param name="option"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        protected static object CheckValue(LoadWithOption option, object x)
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
        /// <param name="info"></param>
        /// <param name="instance"></param>
        /// <param name="values"></param>
        /// <param name="genericArgument"></param>
        protected static void SetValue(PropertyInfo info, object instance, IEnumerable<object> values, Type genericArgument)
        {
            try
            {
                if (instance == null)
                    instance = Activator.CreateInstance(info.PropertyType);
                var o = Activator.CreateInstance(info.PropertyType);
                var methodInfo = info.PropertyType.GetMethod("Add");
                foreach (var value in values)
                {
                    methodInfo.Invoke(o, new[] { value });
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
        /// <typeparam name="T"></typeparam>
        /// <param name="option"></param>
        /// <param name="targetData"></param>
        /// <param name="data"></param>
        protected static void PopulateData(LoadWithOption option, IEnumerable targetData, List<object> data)
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
                        if (propertyPi.PropertyType.IsGenericType && propertyPi.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            if (destProp.PropertyType.IsGenericType && destProp.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                                SetValue(destProp, o, data.Where(x => CheckValue(option, x).ToString() == key), propertyPi.PropertyType);
                            else
                                destProp.SetValue(o, data.FirstOrDefault(x => CheckValue(option, x).ToString() == key), null);
                        }
                        else
                            destProp.SetValue(o, data.FirstOrDefault(x => CheckValue(option, x).ToString() == key), null);
                    }
                    else
                    {
                        foreach (var prop in oType.GetProperties().Where(x => x.GetCustomAttributes(typeof(NavigationPropertyAttribute), false).Any()))
                        {
                            var value = prop.GetValue(o, null);
                            if (value == null)
                                continue;
                            var valueList = value as IEnumerable;
                            if (valueList == null)
                                valueList = new List<object> { value };
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

        protected virtual Type GetPropertyType(PropertyInfo pi)
        {
            return !pi.PropertyType.IsGenericType ? pi.PropertyType : pi.PropertyType.GetGenericArguments()[0];
        }

        protected virtual bool IsNullable(PropertyInfo pi)
        {
            return pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        protected static string Pluralize(Type type)
        {
            if (type == null)
                return "";
            var name = type.Name;
            return Pluralize(name);
        }

        protected static string Pluralize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";
            name = name.ToLower();
            if (name.EndsWith("y"))
                return name.Substring(0, name.Length - 1) + "ies";
            if (name.EndsWith("ss"))
                return name.Substring(0, name.Length - 2) + "sses";
            if (name.EndsWith("s"))
                return name.Substring(0, name.Length - 1) + "ses";
            if (name.EndsWith("x"))
                return name.Substring(0, name.Length - 1) + "xes";
            return name = name + "s";
        }

        protected static string Singularize(Type type)
        {
            if (type == null)
                return "";
            var name = type.Name;
            return Singularize(name);
        }

        protected static string Singularize(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";
            name = name.ToLower();
            if (name.EndsWith("ies"))
                return name.Substring(0, name.Length - 3) + "y";
            if (name.EndsWith("sses"))
                return name.Substring(0, name.Length - 2);
            if (name.EndsWith("ses"))
                return name.Substring(0, name.Length - 2);
            if (name.EndsWith("xes"))
                return name.Substring(0, name.Length - 3) + "x";
            return name = name.Substring(0, name.Length - 1);
        }

        public abstract void Dispose();
    }
}