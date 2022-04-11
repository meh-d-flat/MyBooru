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

        public List<string> Get(string name)
        {
            List<string> tags = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string getTagQuery = "SELECT * FROM Tags WHERE Name LIKE @a";

            using (SQLiteCommand getTag = new SQLiteCommand(getTagQuery, connection))
            {
                getTag.Parameters.AddWithValue("@a", $"{name}%");
                var result = getTag.ExecuteReader();

                if (result.HasRows)
                {
                    tags = new List<string>();
                    while (result.Read())
                        tags.Add(TableCell.MakeEntity<Tag>(TableCell.GetRow(result)).Name);
                }
                result.Dispose();
            }
            connection.Close();
            return tags;
        }
    }
}
