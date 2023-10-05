using MyBooru.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Collections;

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
            for (int i = 0; i < rows.Count; i++)
                entities.Add(MakeEntity<T>(rows[i]));

            return entities;
        }

        static bool IsNavigationProperty(PropertyInfo prop)
        {
            var pt = prop.PropertyType;
            bool definedInThisDll = Type.GetType(pt.Name) != null & pt.IsClass & !pt.IsArray;
            if (definedInThisDll)
                return true;

            bool genWithOneArg = pt.IsGenericType & pt.GetGenericArguments().Length == 1;
            bool isGenCollection = genWithOneArg ? pt.GetInterfaces().Any(x => x != typeof(String) & (x == typeof(ICollection) | x == typeof(IEnumerable) | x == typeof(IList))) : false;
            bool navCollection = isGenCollection ? Type.GetType(pt.GetGenericArguments()[0].Name) != null : false;
            return navCollection;
        }

        public static SQLiteCommand MakeAddCommand<T>(object src, SQLiteConnection conn)
        {
            if (src == null)
                throw new ArgumentNullException("Source object parameter cannot be null");
            
            var srcType = src.GetType();
            var srcProps = srcType.GetProperties();

            if (srcType.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
            {
                srcProps = srcProps.Intersect(typeof(T).GetProperties(), new PropInfoComparer()).ToArray();
                if (srcProps.Length > 0)
                    return null;
            }

            var comm = new SQLiteCommand(conn);
            var text = string.Empty;
            var parms = string.Empty;

            for (int i = 0; i < srcProps.Length; i++)
            {
                if (!IsNavigationProperty(srcProps[i]) && srcProps[i].Name.ToLower() != "id" && Type.GetType(srcProps[i].Name) == null)
                {
                    comm.Parameters.Add(new SQLiteParameter() { ParameterName = $"@p{i}", Value = srcProps[i].GetValue(src) });
                    text += (i < srcProps.Length - 1) ? $"'{srcProps[i].Name}', " : $"'{srcProps[i].Name}'";
                    parms += (i < srcProps.Length - 1) ? $"@p{i}, " : $"@p{i}";

                }
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

        public static int GetUnixTime(this DateTime dt)
        {
            return (int)(dt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static bool IsWindows() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsMacOS() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsLinux() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    public class PropInfoComparer : IEqualityComparer<PropertyInfo>
    {
        public bool Equals(PropertyInfo left, PropertyInfo right)
        {
            bool xNull = left == null;
            bool yNull = right == null;
            if (xNull | yNull)
                throw new ArgumentNullException("Left, right or both parameters were null during PropertyInfo comparison");
            return left.PropertyType == right.PropertyType && left.Name == right.Name;
        }

        public int GetHashCode(PropertyInfo prop)
        {
            if (prop == null)
                return 0;

            int hashPropName = prop.Name == null ? 0 : prop.Name.GetHashCode();
            int hashPropType = prop.GetType().GetHashCode();
            return hashPropName ^ hashPropType;
        }
    }
}
