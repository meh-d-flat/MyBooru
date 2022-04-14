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
            bool rowsChanged = false;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string addTagQuery = "INSERT OR IGNORE INTO Tags ('Name') VALUES (@a)";
            SQLiteCommand addTag = new SQLiteCommand(addTagQuery, connection);
            addTag.Parameters.AddWithValue("@a", name);
            try
            {
                rowsChanged = addTag.ExecuteNonQuery() > 0;
            }
            catch (SQLiteException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                addTag.Dispose();
            }
            //(int)connection.LastInsertRowId
            connection.Close();
            return rowsChanged;
        }
        /*
         * select last_insert_rowid(); connection.LastInsertRowId
         * 
         * insert into Users ("id", "username", "password") values (333, "user", 123);
         * insert into Users ("id", "username", "password") values (222, last_insert_rowid(), 123);
         */
        public List<Tag> Get(string name)
        {
            List<Tag> tags = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string getTagQuery = "SELECT * FROM Tags WHERE Name LIKE @a";

            using (SQLiteCommand getTag = new SQLiteCommand(getTagQuery, connection))
            {
                getTag.Parameters.AddWithValue("@a", $"{name}%");
                var result = getTag.ExecuteReader();

                if (result.HasRows)
                    tags = TableCell.MakeEntities<Tag>(TableCell.GetRows(result));

                result.Dispose();
            }
            connection.Close();
            return tags;
        }

        public List<Media> GetByTag(string tags)
        {
            var medias = new List<Media>();
            var delimitedTags = tags.Split(',');
            var nameTypeValueDict = new Dictionary<string, SQLiteParameter>(delimitedTags.Length);

            for (int i = 0; i < delimitedTags.Length; i++)
                nameTypeValueDict.Add($"@p{i}", new SQLiteParameter(System.Data.DbType.String, value: delimitedTags[i]));
            
            string paramsForQuery = "";
            var paramNames = nameTypeValueDict.Keys.ToList();
            for (int i = 0; i < paramNames.Count; i++)
            {
                if (i == paramNames.Count - 1)
                    paramsForQuery += paramNames[i];
                else
                    paramsForQuery += $"{paramNames[i]},";
            }

            string byTagsQuery =
                $@"SELECT m.*
                FROM MediasTags as mt, Medias as m, Tags as t
                WHERE mt.TagID = t.ID
                AND(t.name IN({paramsForQuery}))
                AND m.id = mt.MediaID
                GROUP BY m.id
                HAVING COUNT(m.id) = {delimitedTags.Length};";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (SQLiteCommand byTag = new SQLiteCommand(byTagsQuery, connection))
            {
                foreach (var item in nameTypeValueDict)
                {
                    item.Value.ParameterName = item.Key;
                    byTag.Parameters.Add(item.Value);
                }
                connection.Open();
                var result = byTag.ExecuteReader();

                if (result.HasRows)
                    medias = TableCell.MakeEntities<Media>(TableCell.GetRows(result));

                connection.Close();
            }
            return medias;
        }
    }
}
