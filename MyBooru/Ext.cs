using System.Collections.Generic;
using System.Data.SQLite;
using System.Reflection;
using System.Runtime.InteropServices;
using System;

namespace MyBooru
{
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

        public static bool IsSQLReaderBusy(this SQLiteCommand c)
        {
            return c.GetType().GetField("_activeReader", BindingFlags.Instance) != null;
        }

        public static bool IsWindows() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static bool IsMacOS() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static bool IsLinux() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        public static T GetRandomMember<T>(this T[] array)
        {
            if (array is null)
                throw new ArgumentNullException("passed array was null");

            return array[new Random().Next(0, array.Length - 1)];
        }
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
