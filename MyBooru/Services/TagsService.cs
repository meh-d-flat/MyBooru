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

        public async Task<Tag> AddTagAsync(string name)
        {
            Tag tag = null;
            bool rowsChanged = false;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string addTagQuery = "INSERT OR IGNORE INTO Tags ('Name') VALUES (@a)";
            SQLiteCommand addTag = new SQLiteCommand(addTagQuery, connection);
            addTag.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = name, DbType = System.Data.DbType.String });
            try
            {
                rowsChanged = await addTag.ExecuteNonQueryAsync() > 0;
            }
            catch (SQLiteException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                await addTag .DisposeAsync();
            }

            if (rowsChanged)
                tag = new Tag() { Id = (int)connection.LastInsertRowId, Name = name };
            await connection.CloseAsync();
            return tag;
        }

        public async Task<List<Tag>> SearchTagAsync(string name)
        {
            List<Tag> tags = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string getTagQuery = "SELECT * FROM Tags WHERE Name LIKE @a";

            using (SQLiteCommand getTag = new SQLiteCommand(getTagQuery, connection))
            {
                getTag.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = $"{name}%", DbType = System.Data.DbType.String });
                var result = await getTag.ExecuteReaderAsync();

                if (result.HasRows)
                    tags = TableCell.MakeEntities<Tag>(TableCell.GetRows(result));

                await result.DisposeAsync();
            }
            await connection.CloseAsync();
            return tags;
        }

        public async Task<int> MediasCountAsync(string tags)
        {
            int count = 0;
            var parameters = MakeParamsList(tags);
            string paramsForQuery = MakeParamsString(parameters, false);

            string byTagsQuery =
                $@"SELECT COUNT(*)
                FROM MediasTags as mt, Medias as m, Tags as t
                WHERE mt.TagID = t.ID
                AND(t.name IN({paramsForQuery}))
                AND m.id = mt.MediaID
                GROUP BY m.id
                HAVING COUNT(m.id) = @a";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (SQLiteCommand byTag = new SQLiteCommand(byTagsQuery, connection))
            {
                byTag.Parameters.Add(new SQLiteParameter { ParameterName = "@a", Value = parameters.Count, DbType = System.Data.DbType.Int32 });
                byTag.Parameters.AddRange(parameters.ToArray());
                await connection.OpenAsync();
                count = Convert.ToInt32(await byTag.ExecuteScalarAsync());

                await connection.CloseAsync();
            }
            return count;
        }

        public async Task<List<Media>> GetMediasByTagsAsync(string tags, int page, int reverse)
        {
            var medias = new List<Media>();
            var parameters = MakeParamsList(tags);
            string tagQuery = MakeParamsString(parameters, true);

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
                HAVING COUNT(Medias.id) = @a
                {(reverse == 1 ? "ORDER BY Medias.Id DESC" : "")}
                LIMIT 20 OFFSET { 20 * (page - 1) };";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (SQLiteCommand tempTable = new SQLiteCommand(tempTableQuery, connection))
            {
                tempTable.Parameters.AddRange(parameters.ToArray());//
                await connection.OpenAsync();//temp table is wiped upon closing connection
                await tempTable.ExecuteNonQueryAsync();
            }

            using (SQLiteCommand byTag = new SQLiteCommand(byTagsQuery, connection))
            {
                byTag.Parameters.Add(new SQLiteParameter { ParameterName = "@a", Value = parameters.Count, DbType = System.Data.DbType.Int32 });
                byTag.Parameters.AddRange(parameters.ToArray());
                var result = await byTag.ExecuteReaderAsync();

                if (result.HasRows)
                    medias = TableCell.MakeEntities<Media>(TableCell.GetRows(result));

                await connection.CloseAsync();
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

        string MakeParamsString(List<SQLiteParameter> parameters, bool parenthesis)
        {
            string paramsForQuery = string.Empty;

            for (int i = 0; i < parameters.Count; i++)
            {
                if (!parenthesis)
                    paramsForQuery += $"{parameters[i].ParameterName}";
                else
                    paramsForQuery += $"({parameters[i].ParameterName})";

                if (i < parameters.Count - 1)
                    paramsForQuery += ",";
            }

            return paramsForQuery;
        }

        public async Task<List<Tag>> AddTagsToMediaAsync(string id, string tags)
        {
            var tagsList = await AddWithCheckAsync(tags);
            await AddToMediaAsync(id, tagsList);
            return tagsList;
        }

        public async Task<List<Tag>> AddWithCheckAsync(string tags)
        {
            var delimited = tags.Split(',').ToList();
            List<Tag> tagList = new List<Tag>();
            var parameters = MakeParamsList(tags);
            string paramsForQuery = MakeParamsString(parameters, false);
            var checkTagsQuery = $"SELECT * FROM Tags WHERE Name IN ({paramsForQuery})";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (SQLiteCommand checkTags = new SQLiteCommand(checkTagsQuery, connection))
            {
                checkTags.Parameters.AddRange(parameters.ToArray());
                await connection.OpenAsync();
                var result = await checkTags.ExecuteReaderAsync();

                if (result.HasRows)
                    tagList = TableCell.MakeEntities<Tag>(TableCell.GetRows(result));

                await connection.CloseAsync();
            }

            if (delimited.Count > tagList.Count)
            {
                var tagNames = tagList.Select(x => x.Name).ToList();
                var outliers = delimited.Except(tagNames).ToList();

                foreach (var item in outliers)
                {
                    var newTag = await AddTagAsync(item);
                    if(newTag != null)
                        tagList.Add(newTag);
                }
            }
            return tagList;
        }

        public async Task AddToMediaAsync(string id, List<Tag> tags)
        {
            int mediaId = -1;
            string fetchIdQuery = $"SELECT Id FROM Medias WHERE Hash = @a";
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);

            using (var fetchId = new SQLiteCommand(fetchIdQuery, connection))
            {
                fetchId.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                await connection.OpenAsync();
                var result = await fetchId.ExecuteReaderAsync();
                if (result.HasRows)
                    while(await result.ReadAsync())
                        mediaId = result.GetInt32(0);
                await connection.CloseAsync();
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
                await connection.OpenAsync();
                await addTags.ExecuteNonQueryAsync();
                await connection.CloseAsync();
            }

        }
    }
}
