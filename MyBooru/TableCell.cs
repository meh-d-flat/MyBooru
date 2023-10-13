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
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MyBooru
{
    /// <summary>
    /// Poor man's entity framework 
    /// </summary>
    public class TableCell
    {
        public string ColumnName { get; private set; }
        public Type Type { get; private set; }
        public object Value { get; private set; }

        public static readonly char[] toTrim = { ',', ' ' };
        static readonly string Sys = "System";
        static readonly Type stringType = typeof(String);
        static readonly Type icollType = typeof(ICollection);
        static readonly Type ienumType = typeof(IEnumerable);
        static readonly Type ilistType = typeof(IList);

        TableCell() { }

        static Dictionary<Type, Func<SQLiteDataReader, int, object>> acts = new()
        {
            { typeof(Int32), (x,y) => x.GetInt32(y) },
            { typeof(String), (x,y) => x.GetString(y) }
        };


        static TableCell[] ReadOut(System.Data.Common.DbDataReader sqlReader, TableCell[] cells)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (Convert.IsDBNull(sqlReader[i]))
                    break;

                cells[i] = new TableCell()
                {
                    ColumnName = sqlReader.GetName(i),
                    Type = sqlReader.GetFieldType(i),
                    Value = sqlReader[i]
                };
            }
            return cells;
        }

        public static async Task<TableCell[]> GetRowAsync(SQLiteDataReader sqlReader)
        {
            return await GetRowAsync(sqlReader as System.Data.Common.DbDataReader);
        }

        public static async Task<TableCell[]> GetRowAsync(System.Data.Common.DbDataReader sqlReader)
        {
            if (!sqlReader.HasRows)
                return null;

            var cells = new TableCell[sqlReader.FieldCount];
            if (await sqlReader.ReadAsync())
                ReadOut(sqlReader, cells);

            await sqlReader.CloseAsync();
            await sqlReader.DisposeAsync();
            return cells;
        }

        public async static Task<List<TableCell[]>> GetRowsAsync(SQLiteDataReader sqlReader)
        {
            return await GetRowsAsync(sqlReader as System.Data.Common.DbDataReader);
        }

        public async static Task<List<TableCell[]>> GetRowsAsync(System.Data.Common.DbDataReader sqlReader)
        {
            if (!sqlReader.HasRows)
                return null;

            var rows = new List<TableCell[]>();
            while (await sqlReader.ReadAsync())
            {
                var cells = new TableCell[sqlReader.FieldCount];
                ReadOut(sqlReader, cells);
                rows.Add(cells);
            }

            await sqlReader.CloseAsync();
            await sqlReader.DisposeAsync();
            return rows;
        }

        public static T MakeEntity<T>(TableCell[] cells)
        {
            if (cells == null)
                throw new ArgumentNullException("'TableCell[] cells' parameter was null");

            if (cells.Length == 0)
                throw new InvalidOperationException("Sequence contains no elements: passed TableCell[] was empty");

            object instance;
            //TODO: optimise for primitives
            if (!TableCell.IsUserDefined<T>() & cells.Length == 1)
            {
                instance = typeof(T) == stringType ? string.Empty : default(T);
                instance = Convert.ChangeType(cells[0].Value, cells[0].Type);
                return (T)instance;
            }

            instance = (T)Activator.CreateInstance(typeof(T));
            var fields = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

            if (cells.Length != fields.Length)
            {
                fields = fields.Where(x => cells.Any(y => y?.ColumnName == x.Name)).ToArray();
            }

            for (int m = 0; m < cells.Length; m++)
            {
                for (int n = 0; n < fields.Length; n++)
                {
                    if (fields[n].Name.Equals(cells[m]?.ColumnName, StringComparison.OrdinalIgnoreCase))
                    {
                        fields[n].SetValue(instance, Convert.ChangeType(cells[m]?.Value, fields[n].PropertyType));
                        break;
                    }
                }
            }
            return (T)instance;
        }

        public static List<T> MakeEntities<T>(List<TableCell[]> rows) where T : class
        {
            if(rows == null || rows.Count == 0)
                return null;

            var entities = new List<T>();
            for (int i = 0; i < rows.Count; i++)
                entities.Add(MakeEntity<T>(rows[i]));

            return entities;
        }

        static bool IsUserDefined<T>()
        {
            return IsUserDefined(typeof(T));
        }

        static bool IsUserDefined(Type t)
        {
            bool one = !t.Namespace.StartsWith(TableCell.Sys);
            bool two = !t.IsPrimitive;
            bool three = t != stringType;
            return one & (two | three);
        }

        static bool IsNavigationProperty(PropertyInfo prop)
        {
            var pt = prop.PropertyType;
            if (IsUserDefined(pt))
                return true;

            bool genWithOneArg = pt.IsGenericType & pt.GetGenericArguments().Length == 1;
            bool isGenCollection = genWithOneArg ? pt.GetInterfaces().Any(x => x != stringType & (x == icollType | x == ienumType | x == ilistType)) : false;
            bool navCollection = isGenCollection ? Type.GetType(pt.GetGenericArguments()[0].Name) == null : false;
            return navCollection;
        }

        public static SQLiteCommand MakeAddCommand<T>(object source, SQLiteConnection conn)
        {
            if (source == null)
                throw new ArgumentNullException("Source object parameter cannot be null");

            //TODO: handle collections and arrays of model types (heh)
            if (!TableCell.IsUserDefined<T>())
                return null;

            var srcType = source.GetType();
            var srcProps = srcType.GetProperties();

            if (srcType.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
            {
                //TODO: check if property type of the existing model class is System.Object
                srcProps = srcProps.Intersect(typeof(T).GetProperties(), new PropInfoComparer()).ToArray();
                if (srcProps.Length == 0)
                    return null;
            }

            var comm = new SQLiteCommand(conn);
            var text = string.Empty;
            var parms = string.Empty;

            for (int i = 0; i < srcProps.Length; i++)
            {
                if (IsNavigationProperty(srcProps[i]))
                    continue;

                if (srcProps[i].Name.ToLower() != "id")
                {
                    comm.Parameters.Add(new SQLiteParameter() { ParameterName = $"@p{i}", Value = srcProps[i].GetValue(source) });
                    text += $"'{srcProps[i].Name}', ";
                    parms += $"@p{i}, ";
                }
            }

            text = text.TrimEnd(toTrim);
            parms = parms.TrimEnd(toTrim);

            comm.CommandText = $"INSERT INTO {typeof(T).Name}s ({text}) VALUES ({parms})";

            return comm;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} {2}", ColumnName, Type, Value);
        }
    }
}
