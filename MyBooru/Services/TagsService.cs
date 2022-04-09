using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Services
{
    public class TagsService : Contracts.ITagsService
    {
        readonly IConfiguration config;

        public TagsService(IConfiguration configuration)
        {
            config = configuration;
        }

        public bool Add(string name)
        {
            bool result = false;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string addTagQuery = "INSERT INTO Tags ('Name') VALUES (@a)";
            SQLiteCommand addTag = new SQLiteCommand(addTagQuery, connection);
            addTag.Parameters.AddWithValue("@a", name);
            try
            {
                addTag.ExecuteNonQuery();
            }
            catch (SQLiteException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                addTag.Dispose();
            }
            connection.Close();
            result = true;
            return result;
        }

        public void Get()
        {
            throw new NotImplementedException();
        }

        public string Get(string name)
        {
            string tag = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string getFileQuery = "SELECT * FROM Tags WHERE Name = @a";

            using (SQLiteCommand getFile = new SQLiteCommand(getFileQuery, connection))
            {
                getFile.Parameters.AddWithValue("@a", name);
                var result = getFile.ExecuteReader();

                if (result.HasRows)
                {
                    while (result.Read())
                        tag = TableCell.MakeEntity<Tag>(TableCell.GetRow(result)).Name;
                }
                result.Dispose();
            }
            connection.Close();
            return tag;
        }
    }
}
