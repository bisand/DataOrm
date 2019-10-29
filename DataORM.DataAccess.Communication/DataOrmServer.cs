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
            DateTimeFormats = new[] {"yyyyMMdd", "yyyy-MM-dd", "dd.MM.yyyy", "yyyyMMddHHmmss", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss"};
            Parameters = new List<DbParameter>();
            LoadWithOptions = new Dictionary<LoadWithOption, List<object>>();
        }

        /// <summary>
        ///     Format used when parsing and settings values of type DateTime.
        /// </summary>
        public string[] DateTimeFormats { get; set; }

        public List<DbParameter> Parameters { get; set; }

        public static IDataAccess CreateSession(SessionType sessionType, string connectionString)
        {
            switch (sessionType)
            {
                case SessionType.SqlServer:
                    var sqlServer = new SqlServer(connectionString);
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
                    var sqlServer = new SqlServer(connection as SqlConnection);
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
                    var type = typeof (T);
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
                if (pi.GetCustomAttributes(typeof (NavigationPropertyAttribute), false).Any())
                    continue;
                var attribute = pi.GetCustomAttributes(typeof (DatabaseFieldAttribute), false).Cast<DatabaseFieldAttribute>().FirstOrDefault();
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
                if (outputType == typeof (string))
                    return value.ToString();
                if (outputType == typeof (Guid))
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

            if (GetPropertyType(pi) == typeof (DateTime))
            {
                if (!IsNullable(pi) && value is DateTime &&
                    (value is DateTime ? (DateTime) value : new DateTime()) == DateTime.MinValue)
                    return "cast(0 as datetime)";

                return string.Format("'{0:yyyy-MM-dd HH:mm:ss}'", value);
            }

            if (GetPropertyType(pi) == typeof (bool))
            {
                return ((value is bool && (bool) value)) ? "1" : "0";
            }

            if (GetPropertyType(pi) == typeof (int))
            {
                return ((value is int ? (int) value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof (short))
            {
                return ((value is short ? (short) value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof (long))
            {
                return ((value is long ? (long) value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof (byte))
            {
                return ((value is byte ? (byte) value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof (decimal))
            {
                return ((value is decimal ? (decimal) value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof (float))
            {
                return ((value is float ? (float) value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof (double))
            {
                return ((value is double ? (double) value : 0)).ToString(CultureInfo.InvariantCulture);
            }

            if (GetPropertyType(pi) == typeof (Guid))
            {
                if (!IsNullable(pi) && value is Guid && (value is Guid ? (Guid) value : new Guid()) == Guid.Empty)
                    return string.Format("'{0}'", Guid.Empty);
                return string.Format("'{0}'", value);
            }

            return string.IsNullOrEmpty(pi.GetValue(client, null) as string)
                ? "null"
                : string.Format("'{0}'", pi.GetValue(client, null).ToString().Replace("'", "''"));
        }

        protected virtual Type GetPropertyType(PropertyInfo pi)
        {
            return !pi.PropertyType.IsGenericType ? pi.PropertyType : pi.PropertyType.GetGenericArguments()[0];
        }

        protected virtual bool IsNullable(PropertyInfo pi)
        {
            return pi.PropertyType.IsGenericType && pi.PropertyType.GetGenericTypeDefinition() == typeof (Nullable<>);
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