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

        public Tag Add(string name)
        {
            Tag tag = null;
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

            if (rowsChanged)
                tag = new Tag() { Id = (int)connection.LastInsertRowId, Name = name };
            connection.Close();
            return tag;
        }

        public List<Tag> SearchTag(string name)
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

        public int MediasCount(string tags)
        {
            int count = 0;
            var parameters = MakeParamsList(tags);
            string paramsForQuery = ParamsString(parameters);

            string byTagsQuery =
                $@"SELECT COUNT(*)
                FROM MediasTags as mt, Medias as m, Tags as t
                WHERE mt.TagID = t.ID
                AND(t.name IN({paramsForQuery}))
                AND m.id = mt.MediaID
                GROUP BY m.id
                HAVING COUNT(m.id) = {parameters.Count}";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (SQLiteCommand byTag = new SQLiteCommand(byTagsQuery, connection))
            {
                byTag.Parameters.AddRange(parameters.ToArray());
                connection.Open();
                count = Convert.ToInt32(byTag.ExecuteScalar());

                connection.Close();
            }
            return count;
        }

        public List<Media> GetMediasByTags(string tags, int page, int reverse)
        {
            var medias = new List<Media>();
            var parameters = MakeParamsList(tags);
            string tagQuery = Decorate(tags);

            string tempTableQuery =
                $@"CREATE TEMP TABLE Search(tag);
                INSERT INTO Search VALUES {tagQuery};";

            string byTagsQuery =
                $@"SELECT *
                FROM Medias
                JOIN MediasTags on Medias.ID = MediasTags.MediaID
                JOIN Tags on Tags.ID = MediasTags.TagID
                JOIN Search on Tags.name = Search.tag
                GROUP BY Medias.ID
                HAVING COUNT(Medias.id) = {parameters.Count}
                {(reverse == 1 ? "ORDER BY Medias.Id DESC" : "")}
                LIMIT 20 OFFSET { 20 * (page - 1) };";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (SQLiteCommand tempTable = new SQLiteCommand(tempTableQuery, connection))
            {
                connection.Open();//temp table is wiped upon closing connection
                tempTable.ExecuteNonQuery();
            }

            using (SQLiteCommand byTag = new SQLiteCommand(byTagsQuery, connection))
            {
                byTag.Parameters.AddRange(parameters.ToArray());
                var result = byTag.ExecuteReader();

                if (result.HasRows)
                    medias = TableCell.MakeEntities<Media>(TableCell.GetRows(result));

                connection.Close();
            }
            return medias;
        }

        public List<SQLiteParameter> MakeParamsList(string tags)
        {
            if (tags.EndsWith(","))
                tags = tags.Remove(tags.Length - 1, 1);

            var delimited = tags.Split(',');
            var parameters = new List<SQLiteParameter>(delimited.Length);
            for (int i = 0; i < delimited.Length; i++)
            {
                parameters.Add(new SQLiteParameter()
                {
                    ParameterName = $"@p{i}",
                    DbType = System.Data.DbType.String,
                    Value = delimited[i]
                });
            }
            return parameters;
        }

        public string Decorate(string tags)
        {
            if (tags.EndsWith(","))
                tags = tags.Remove(tags.Length - 1, 1);

            var delimited = tags.Split(',');
            for (int i = 0; i < delimited.Length; i++)
                delimited[i] = $"('{delimited[i]}')";

            return String.Join(",", delimited);
        }

        public string ParamsString(List<SQLiteParameter> parameters)
        {
            string paramsForQuery = "";
            for (int i = 0; i < parameters.Count; i++)
            {
                paramsForQuery += $"'{parameters[i].Value}'";
                if (i < parameters.Count - 1)
                    paramsForQuery += ",";
            }
            return paramsForQuery;
        }

        public List<Tag> AddTagsToMedia(string id, string tags)
        {
            var tagsList = AddWithCheck(tags);
            AddToMedia(id, tagsList);
            return tagsList;
        }

        public List<Tag> AddWithCheck(string tags)
        {
            var delimited = tags.Split(',').ToList();
            List<Tag> tagList = new List<Tag>();
            var parameters = MakeParamsList(tags);
            string paramsForQuery = ParamsString(parameters);
            var checkTagsQuery = $"SELECT * FROM Tags WHERE Name IN ({paramsForQuery})";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (SQLiteCommand checkTags = new SQLiteCommand(checkTagsQuery, connection))
            {
                checkTags.Parameters.AddRange(parameters.ToArray());
                connection.Open();
                var result = checkTags.ExecuteReader();

                if (result.HasRows)
                    tagList = TableCell.MakeEntities<Tag>(TableCell.GetRows(result));

                connection.Close();
            }

            if (delimited.Count > tagList.Count)
            {
                var tagNames = tagList.Select(x => x.Name).ToList();
                var outliers = delimited.Except(tagNames).ToList();

                foreach (var item in outliers)
                {
                    var newTag = Add(item);
                    if(newTag != null)
                        tagList.Add(newTag);
                }
            }
            return tagList;
        }

        public void AddToMedia(string id, List<Tag> tags)
        {
            int mediaId = -1;
            string fetchIdQuery = $"SELECT Id FROM Medias WHERE Hash = '{id}'";
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (var fetchId = new SQLiteCommand(fetchIdQuery, connection))
            {
                connection.Open();
                var result = fetchId.ExecuteReader();
                if (result.HasRows)
                    while(result.Read())
                        mediaId = result.GetInt32(0);
                connection.Close();
            }

            string values = "";
            for (int i = 0; i < tags.Count; i++)
            {
                values += $"({mediaId}, {tags[i].Id})";
                if (i < tags.Count - 1)
                    values += ",";
            }
            string addTagsQuery = $"INSERT OR IGNORE INTO MediasTags VALUES {values}";

            using (var addTags = new SQLiteCommand(addTagsQuery, connection))
            {
                connection.Open();
                addTags.ExecuteNonQuery();
                connection.Close();
            }

        }
    }
}
