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
        private readonly MySqlConnection _connection;
        private string _connectionString;
        private bool _disposed;

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

        protected override int MaxBatchSIze { get => 0x10000; }
        protected override string InsertStatement { get => "INSERT INTO {0} ({1}) VALUES ({2});{3}"; }
        protected override string UpdateStatement { get => "UPDATE {0} SET {1} WHERE {2};{3}"; }
 
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
        /// <param name="option"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected override string GetQuery(LoadWithOption option, IEnumerable<string> value)
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

        /// <summary>
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="option"></param>
        /// <param name="commandType"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public override IDbCommand CreateCommand(string sql, LoadWithOption option = null, CommandType commandType = CommandType.Text, List<DbParameter> parameters = null)
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
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal override IList<FieldDefinition> GetTableColumnsFromDatabase<T>()
        {
            var tType = typeof (T);
            return GetTableColumnsFromDatabase(tType.Name);
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        internal override IList<FieldDefinition> GetTableColumnsFromDatabase(string entityName, IDbCommand command = null)
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