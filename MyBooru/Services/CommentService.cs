using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class CommentService : ICommentService
    {
        readonly IConfiguration config;
        private readonly IQueryService queryService;

        public CommentService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
        }

        public async Task<List<Comment>> GetCommentsOnMediaAsync(string pistureHash, CancellationToken ct)
        {
            List<Comment> comments = new();
            comments.Add(new Comment() { ID = Int32.MinValue });
            return await queryService.QueryTheDbAsync<List<Comment>>(async x =>
            {
                x.Parameters.AddNew("@p", pistureHash, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntities<Comment>(await TableCell.GetRowsAsync(result));
            }, "SELECT * FROM Comments WHERE MediaID = @p");
        }

        public async Task<List<Comment>> GetMyCommentsAsync(string sessionId, string email, CancellationToken ct)
        {
            List<Comment> comments = new();
            comments.Add(new Comment() { ID = Int32.MinValue });
            return await queryService.QueryTheDbAsync<List<Comment>>(async x =>
            {
                x.Parameters.AddNew("@a", sessionId, System.Data.DbType.String);
                x.Parameters.AddNew("@b", email, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync();
                return TableCell.MakeEntities<Comment>(await TableCell.GetRowsAsync(result));
            }, "SELECT * FROM Comments WHERE User = (SELECT Username From Tickets WHERE ID = @a) AND User = (SELECT Username From Users WHERE Email = @b)");
        }

        public async Task<Comment> GetCommentAsync(int id, CancellationToken ct)
        {
            Comment comment = new();
            return await queryService.QueryTheDbAsync<Comment>(async x => {
                x.Parameters.AddNew("@p", id, System.Data.DbType.Int32);
                var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntity<Comment>(await TableCell.GetRowAsync(result));
            }, "SELECT * FROM Comments WHERE ID = @p");
        }

        public async Task<int> PostCommentAsync(string username, string commentText, string pictureHash)
        {
            int result = -1;
            var comment = new Comment() {  User = username, MediaID = pictureHash, Text = commentText, Timestamp = DateTime.UtcNow.GetUnixTime() };
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            var addComment = TableCell.MakeAddCommand<Comment>(comment, connection);
            try
            {
                result = await addComment.ExecuteNonQueryAsync();
            }
            catch (SQLiteException ex)
            {
                 result = 0;
            }
            finally
            {
                await addComment.DisposeAsync();
            }
            result = (int)connection.LastInsertRowId;
            await connection.CloseAsync();
            return result;
        }

        public async Task<int> RemoveCommentAsync(int id, string sessionId, string email)
        {
            return await queryService.QueryTheDbAsync<int>(async x =>
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                x.Parameters.AddNew("@b", sessionId, System.Data.DbType.String);
                x.Parameters.AddNew("@c", email, System.Data.DbType.String);
                return await x.ExecuteNonQueryAsync();
            }, @"DELETE FROM Comments WHERE ID = @a
                AND Comments.User = (SELECT Username FROM Tickets WHERE ID = @b AND Username = (SELECT Username From Users WHERE Email = @c))
                OR Comments.MediaID = (SELECT Medias.ID FROM Medias WHERE Medias.Uploader IN (SELECT Username FROM Tickets WHERE ID = @b AND Username = (SELECT Username From Users WHERE Email = @c)))");
        }
    }
}
