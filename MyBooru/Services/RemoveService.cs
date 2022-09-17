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

        public async Task<string> RemoveAsync(string id)
        {
            string removed = "deleted";
            Media file = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();

            string removeFileQuery = "SELECT * FROM Medias WHERE Hash = @a";

            using (SQLiteCommand removeFile = new SQLiteCommand(removeFileQuery, connection))
            {
                removeFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                var result = await removeFile.ExecuteReaderAsync();

                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        file = TableCell.MakeEntity<Media>(TableCell.GetRow(result));
                }
                await result.DisposeAsync();
            }

            try
            {
                await Task.Run(() => Directory.Delete(Path.GetFullPath(file.Path).Replace(Path.GetFileName(file.Path), ""), true));
            }
            catch (Exception ex)
            {
                removed = $"error: {ex.GetType()} {ex.Message}";
            }

            string removeEntryQuery = "DELETE FROM MediasTags WHERE MediaID = (SELECT ID FROM Medias WHERE Hash = @a);DELETE FROM Medias WHERE Hash = @a;";
            using (SQLiteCommand removeEntry = new SQLiteCommand(removeEntryQuery, connection))
            {
                removeEntry.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                try
                {
                    await removeEntry .ExecuteNonQueryAsync();
                }
                catch (SQLiteException ex)
                {
                    removed = $"error: {ex.GetType()} {ex.Message}";
                }
                finally
                {
                    await removeEntry.DisposeAsync();
                }
                await connection.CloseAsync();
            }

                return removed;
        }
    }
}
