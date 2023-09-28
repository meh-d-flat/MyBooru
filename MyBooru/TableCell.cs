using MyBooru.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MyBooru
{
    /// <summary>
    /// Poor man's entity framework 
    /// </summary>
    public class TableCell
    {
        public int ColumnNumber { get; private set; }
        public string ColumnName { get; private set; }
        public Type Type { get; private set; }
        public object Value { get; private set; }

        TableCell() { }

        public static TableCell[] GetRow(SQLiteDataReader sqlReader)
        {
            var cells = new TableCell[sqlReader.FieldCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new TableCell()
                {
                    ColumnName = sqlReader.GetName(i),
                    ColumnNumber = sqlReader.GetOrdinal(sqlReader.GetName(i)),
                    Type = sqlReader.GetFieldType(i),
                    Value = sqlReader[i]
                };
            }
            return cells;
        }

        public static TableCell[] GetRow(System.Data.Common.DbDataReader sqlReader)
        {
            var cells = new TableCell[sqlReader.FieldCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new TableCell()
                {
                    ColumnName = sqlReader.GetName(i),
                    ColumnNumber = sqlReader.GetOrdinal(sqlReader.GetName(i)),
                    Type = sqlReader.GetFieldType(i),
                    Value = sqlReader[i]
                };
            }
            return cells;
        }

        public async static Task<List<TableCell[]>> GetRowsAsync(SQLiteDataReader sqlReader)
        {
            var rows = new List<TableCell[]>();
            while (await sqlReader.ReadAsync())
                rows.Add(GetRow(sqlReader));

            return rows;
        }

        public async static Task<List<TableCell[]>> GetRowsAsync(System.Data.Common.DbDataReader sqlReader)
        {
            var rows = new List<TableCell[]>();
            while (await sqlReader.ReadAsync())
                rows.Add(GetRow(sqlReader));

            return rows;
        }

        public static T MakeEntity<T>(TableCell[] cells) where T : class
        {
            T instance = (T)Activator.CreateInstance(typeof(T));
            var fields = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int n = 0; n < fields.Length; n++)
            {
                for (int m = 0; m < cells.Length; m++)
                {
                    if (fields[n].Name.Equals(cells[m].ColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        fields[n].SetValue(instance, Convert.ChangeType(cells[m].Value, fields[n].PropertyType));
                        break;
                    }
                }
            }
            return instance;
        }

        public static List<T> MakeEntities<T>(List<TableCell[]> rows) where T : class
        {
            var entities = new List<T>();
            foreach (var row in rows)
                entities.Add(MakeEntity<T>(row));

            return entities;
        }

        public static SQLiteCommand MakeAddCommand<T>(object src, SQLiteConnection conn)
        {
            var list = new List<TableCell>();
            var text = string.Empty;
            var parms = string.Empty;
            //var tableName = $"{src.GetType().Name}s";
            src.GetType().GetProperties().ToList().ForEach(x =>
            {
                if (!x.PropertyType.IsGenericType && x.Name.ToLower() != "id") // || !x.PropertyType.FullName.Contains('`') - yeah lol
                    list.Add(new TableCell { Value = x.GetValue(src), ColumnName = x.Name });
            });
            var comm = new SQLiteCommand(conn);
            for (int i = 0; i < list.Count; i++)
            {
                comm.Parameters.Add(new SQLiteParameter() { ParameterName = $"@p{i}", Value = list[i].Value });
                text += (i < list.Count - 1) ? $"'{list[i].ColumnName}', " : $"'{list[i].ColumnName}'";
                parms += (i < list.Count - 1) ? $"@p{i}, " : $"@p{i}";
            }
            comm.CommandText = $"INSERT INTO {typeof(T).Name}s ({text}) VALUES ({parms})";

            return comm;
        }


        public override string ToString()
        {
            return String.Format("{0} {1} {2} {3}", ColumnName, ColumnNumber, Type, Value);
        }
    }

    public static class Ext
    {
        public static int AddNew(this SQLiteParameterCollection collection, string parameter, object val, System.Data.DbType type)
        {
            return collection.Add(new SQLiteParameter() { ParameterName = parameter, Value = val, DbType = type });
        }
    }
}
