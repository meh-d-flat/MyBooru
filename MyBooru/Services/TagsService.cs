using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class TagsService : Contracts.ITagsService
    {
        readonly IConfiguration config;
        private readonly IQueryService queryService;

        public TagsService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
        }

        public async Task<Tag> AddTagAsync(string name)
        {
            Tag tag = null;
            bool rowsChanged = false;

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string addTagQuery = "INSERT OR IGNORE INTO Tags ('Name') VALUES (@a)";
            using (SQLiteCommand addTag = new SQLiteCommand(addTagQuery, connection))
            {
                addTag.Parameters.AddNew("@a", name, System.Data.DbType.String);
                rowsChanged = await addTag.ExecuteNonQueryAsync() > 0;
            }

            if (rowsChanged)
                tag = new Tag() { Id = (int)connection.LastInsertRowId, Name = name };
            await connection.CloseAsync();
            return tag;
        }

        public async Task<List<Tag>> SearchTagAsync(string name)
        {
            List<Tag> tags = null;
            await queryService.QueryTheDb<List<Tag>>(async x => 
            {
                x.Parameters.AddNew("@a", $"%{name}%", System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync();

                if (result.HasRows)
                    tags = TableCell.MakeEntities<Tag>(await TableCell.GetRowsAsync(result));

                return tags;
            }, "SELECT * FROM Tags WHERE Name LIKE @a");
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

            count = await queryService.QueryTheDb<int>(async x => 
            {
                x.Parameters.AddNew("@a", parameters.Count, System.Data.DbType.Int32);
                x.Parameters.AddRange(parameters.ToArray());
                return Convert.ToInt32(await x.ExecuteScalarAsync());
            }, byTagsQuery);
            return count;
        }

        public async Task<List<Media>> GetMediasByTagsAsync(string tags, int page, int reverse)
        {
            var medias = new List<Media>();
            var parameters = MakeParamsList(tags);
            string tagQuery = MakeParamsString(parameters, true);

            string byTagsQuery =
                $@"CREATE TEMP TABLE Search(tag);
                INSERT INTO Search VALUES {tagQuery};
                SELECT *
                FROM Medias
                JOIN MediasTags on Medias.ID = MediasTags.MediaID
                JOIN Tags on Tags.ID = MediasTags.TagID
                JOIN Search on Tags.name = Search.tag
                GROUP BY Medias.ID
                HAVING COUNT(Medias.id) = @a
                {(reverse == 1 ? "ORDER BY Medias.Id DESC" : "")}
                LIMIT 20 OFFSET {20 * (page - 1)};";

            await queryService.QueryTheDb<List<Media>>(async x => 
            {
                x.Parameters.Add(new SQLiteParameter { ParameterName = "@a", Value = parameters.Count, DbType = System.Data.DbType.Int32 });
                x.Parameters.AddRange(parameters.ToArray());
                var result = await x.ExecuteReaderAsync();
                if (result.HasRows)
                    medias = TableCell.MakeEntities<Media>(await TableCell.GetRowsAsync(result));
                return null;
            }, byTagsQuery);
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
                    Value = delimited[i].Trim()
                });
            }
            return parameters;
        }

        string MakeParamsString(List<SQLiteParameter> parameters, bool parenthesis)
        {
            string paramsForQuery = string.Empty;

            for (int i = 0; i < parameters.Count; i++)
            {
                paramsForQuery += parenthesis ? $"({parameters[i].ParameterName})" : parameters[i].ParameterName;
                paramsForQuery += (i < parameters.Count - 1) ? "," : string.Empty;
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

            tagList = await queryService.QueryTheDb<List<Tag>>(async x => 
            {
                x.Parameters.AddRange(parameters.ToArray());
                var result = await x.ExecuteReaderAsync();
                return result.HasRows ? TableCell.MakeEntities<Tag>(await TableCell.GetRowsAsync(result)) : tagList;
            }, $"SELECT * FROM Tags WHERE Name IN ({paramsForQuery})");

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

            mediaId = await queryService.QueryTheDb<int>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync();
                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        mediaId = result.GetInt32(0);
                    return mediaId;
                }
                else
                    return -1;
            }, "SELECT Id FROM Medias WHERE Hash = @a");

            string values = "";
            for (int i = 0; i < tags.Count; i++)
            {
                values += $"({mediaId}, {tags[i].Id})";
                if (i < tags.Count - 1)
                    values += ",";
            }

            await queryService.QueryTheDb<Task>(async x => 
            {
                await x.ExecuteNonQueryAsync();
                return Task.CompletedTask;
            }, $"INSERT OR IGNORE INTO MediasTags VALUES {values}");
        }
    }
}
