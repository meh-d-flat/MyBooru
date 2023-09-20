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

        public static List<TableCell[]> GetRows(SQLiteDataReader sqlReader)
        {
            var rows = new List<TableCell[]>();
            while (sqlReader.Read())
                rows.Add(GetRow(sqlReader));

            return rows;
        }

        public static List<TableCell[]> GetRows(System.Data.Common.DbDataReader sqlReader)
        {
            var rows = new List<TableCell[]>();
            while (sqlReader.Read())
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
