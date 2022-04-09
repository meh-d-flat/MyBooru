using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
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
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string removeFileQuery = "DELETE FROM Medias WHERE Hash = @a";
            using (SQLiteCommand removeFile = new SQLiteCommand(removeFileQuery, connection))
            {
                removeFile.Parameters.AddWithValue("@a", id);
                try
                {
                    removeFile.ExecuteNonQuery();
                }
                catch (SQLiteException ex)
                {
                    removed = $"error: {ex.GetType()} {ex.Message}";
                }
                finally
                {
                    removeFile.Dispose();
                }
                connection.Close();
            }
            return removed;
        }
    }
}
