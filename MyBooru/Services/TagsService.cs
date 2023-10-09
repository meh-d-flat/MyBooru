using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading;
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

        public async Task<Tag> AddTagAsync(string name, string username)
        {
            Tag tag = null;
            bool rowsChanged = false;

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            var cmd = TableCell.MakeAddCommand<Tag>(new { Name = name, User = username, @DateTime = DateTime.UtcNow.GetUnixTime() }, connection);
            //string addTagQuery = "INSERT OR IGNORE INTO Tags ('Name') VALUES (@a)";
            //using (SQLiteCommand addTag = new SQLiteCommand(addTagQuery, connection))
            //{
            //    addTag.Parameters.AddNew("@a", name, System.Data.DbType.String);
            //    rowsChanged = await addTag.ExecuteNonQueryAsync() > 0;
            //}

            try
            {
                rowsChanged = await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine(ex);
            }

            if (rowsChanged)
                tag = new Tag() { Id = (int)connection.LastInsertRowId, Name = name };
            await connection.CloseAsync();
            return tag;
        }

        public async Task<List<Tag>> SearchTagAsync(string name, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<List<Tag>>(async x => 
            {
                x.Parameters.AddNew("@a", $"%{name}%", System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntities<Tag>(await TableCell.GetRowsAsync(result));
            }, "SELECT * FROM Tags WHERE Name LIKE @a");
        }

        public async Task<int> MediasCountAsync(string tags, CancellationToken ct)
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

            count = await queryService.QueryTheDbAsync<int>(async x => 
            {
                x.Parameters.AddNew("@a", parameters.Count, System.Data.DbType.Int32);
                x.Parameters.AddRange(parameters.ToArray());
                return Convert.ToInt32(await x.ExecuteScalarAsync(ct));
            }, byTagsQuery);
            return count;
        }

        public async Task<List<Media>> GetMediasByTagsAsync(string tags, int page, int reverse, CancellationToken ct)
        {
            var medias = new List<Media>();
            var parameters = MakeParamsList(tags);
            string tagQuery = MakeParamsString(parameters, true);

            string byTagsQuery =
                $@"CREATE TEMP TABLE Search(tag);
                INSERT INTO Search VALUES {tagQuery};
                SELECT Thumb, Hash
                FROM Medias
                JOIN MediasTags on Medias.ID = MediasTags.MediaID
                JOIN Tags on Tags.ID = MediasTags.TagID
                JOIN Search on Tags.name = Search.tag
                GROUP BY Medias.ID
                HAVING COUNT(Medias.id) = @a
                {(reverse == 1 ? "ORDER BY Medias.Id DESC" : "")}
                LIMIT 20 OFFSET {20 * (page - 1)};";

            return await queryService.QueryTheDbAsync<List<Media>>(async x => 
            {
                x.Parameters.Add(new SQLiteParameter { ParameterName = "@a", Value = parameters.Count, DbType = System.Data.DbType.Int32 });
                x.Parameters.AddRange(parameters.ToArray());
                var result = await x.ExecuteReaderAsync(ct);
                return result.HasRows ? TableCell.MakeEntities<Media>(await TableCell.GetRowsAsync(result)) : medias;
            }, byTagsQuery);
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

        public async Task<List<Tag>> AddTagsToMediaAsync(string id, string tags, string username)
        {
            var tagsList = await AddWithCheckAsync(tags, username);
            await AddToMediaAsync(id, tagsList, username);
            return tagsList;
        }

        public async Task<List<Tag>> AddWithCheckAsync(string tags, string username)
        {
            var delimited = tags.Split(',').ToList();
            List<Tag> tagList = new List<Tag>();
            var parameters = MakeParamsList(tags);
            string paramsForQuery = MakeParamsString(parameters, false);

            tagList = await queryService.QueryTheDbAsync<List<Tag>>(async x => 
            {
                x.Parameters.AddRange(parameters.ToArray());
                var result = await x.ExecuteReaderAsync();
                return result.HasRows ? TableCell.MakeEntities<Tag>(await TableCell.GetRowsAsync(result)) : tagList;
            }, $"SELECT * FROM Tags WHERE Name IN ({paramsForQuery})");

            if (delimited.Count > tagList.Count)
            {
                var tagNames = tagList.Select(x => x.Name).ToList();
                var outliers = delimited.Except(tagNames).ToList();

                for (int i = 0; i < outliers.Count; i++)
                {
                    var newTag = await AddTagAsync(outliers[i], username);
                    if(newTag != null)
                        tagList.Add(newTag);
                }
            }
            return tagList;
        }

        public async Task AddToMediaAsync(string id, List<Tag> tags, string username)
        {
            int mediaId = -1;

            mediaId = await queryService.QueryTheDbAsync<int>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                return Convert.ToInt32(await x.ExecuteScalarAsync());
            }, "SELECT Id FROM Medias WHERE Hash = @a");

            string values = "";
            for (int i = 0; i < tags.Count; i++)
                values += $"({mediaId}, {tags[i].Id}, {username}, {DateTime.UtcNow.GetUnixTime()})";

            values = values.TrimEnd(TableCell.toTrim);
            await queryService.QueryTheDbAsync<Task>(async x => 
            {
                await x.ExecuteNonQueryAsync();
                return Task.CompletedTask;
            }, $"INSERT OR IGNORE INTO MediasTags VALUES {values}");
        }
    }
}
