using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Services
{
    public class RemoveService : Contracts.IRemoveService
    {
        readonly IConfiguration config;

        public RemoveService(IConfiguration configuration)
        {
            config = configuration;
        }

        public string Remove(string id)
        {
            string removed = "deleted";
            Media file = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();

            string readPathQuery = "SELECT * FROM Medias WHERE Hash = @a";

            using (SQLiteCommand removeFile = new SQLiteCommand(readPathQuery, connection))
            {
                removeFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                var result = removeFile.ExecuteReader();

                if (result.HasRows)
                {
                    while (result.Read())
                        file = TableCell.MakeEntity<Media>(TableCell.GetRow(result));
                }
                result.Dispose();
            }

            //File.Delete(Path.GetFullPath(file.Path).Replace(Path.GetFileName(file.Path), ""));
            Directory.Delete(Path.GetFullPath(file.Path).Replace(Path.GetFileName(file.Path), ""), true);

            string removeEntryQuery = "DELETE FROM MediasTags WHERE MediaID = (SELECT ID FROM Medias WHERE Hash = @a);DELETE FROM Medias WHERE Hash = @b;";
            using (SQLiteCommand removeEntry = new SQLiteCommand(removeEntryQuery, connection))
            {
                removeEntry.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                removeEntry.Parameters.Add(new SQLiteParameter() { ParameterName = "@b", Value = id, DbType = System.Data.DbType.String });
                try
                {
                    removeEntry.ExecuteNonQuery();//5012FF14CA8720E590B14326E2356CC8E7A98D2C9E1F887A47AA7683A5DD71D8
                }
                catch (SQLiteException ex)
                {
                    removed = $"error: {ex.GetType()} {ex.Message}";
                }
                finally
                {
                    removeEntry.Dispose();
                }
                connection.Close();
            }

                return removed;
        }
    }
}
