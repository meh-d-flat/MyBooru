using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;
using static MyBooru.Services.Contracts;
using System.Threading;

namespace MyBooru.Services
{
    public class CheckService : Contracts.ICheckService
    {
        readonly IConfiguration config;
        private readonly IQueryService queryService;
        private readonly string path;

        public CheckService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
            path = config.GetValue<string>("SQLiteDBPath");
        }

        public async Task<int> MediasCountAsync(CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<int>(async x =>
             {
                 return Convert.ToInt32(await x.ExecuteScalarAsync(ct));
             }, "SELECT COUNT(*) FROM Medias");
        }

        public async Task<bool> CheckMediaExistsAsync(string id, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@p", id, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteScalarAsync(ct));
            }, "SELECT COUNT(*) FROM Medias WHERE Hash = @p");
        }

        public async Task<bool> DBSetupAsync()
        {
            try
            {
                await CheckDbFileExists();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            return await CreateDbTablesAsync();
        }

        async Task CheckDbFileExists()
        {
            if (File.Exists(path))
            {
                await Task.Run(() => File.Copy(path, DateTime.Now.ToString() + "." + path));
                return;
            }

            await Task.Run(() => SQLiteConnection.CreateFile(path));
        }

        //SELECT COUNT(name) FROM sqlite_master WHERE type='table' AND name='';
        async Task<bool> CreateDbTablesAsync()
        {
            bool created = false;
            using (SQLiteConnection connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value))
            {
                await connection.OpenAsync();
                string createTableQuery =
                @"PRAGMA foreign_keys=off;
                 BEGIN TRANSACTION;
                 CREATE TABLE IF NOT EXISTS Medias (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
            	    Name VARCHAR(255),
            	    Hash VARCHAR(255) NOT NULL,
            	    Type VARCHAR(255) NOT NULL,
                    Path VARCHAR(255),
                    Thumb VARCHAR(255),
                    Uploader VARCHAR(255),
                    Timestamp INTEGER DEFAULT 1 NOT NULL,
                    FOREIGN KEY(Uploader) REFERENCES Users(Username) ON DELETE CASCADE,
                    CONSTRAINT HashAlreadyExists UNIQUE(Hash)
                );
                CREATE TABLE IF NOT EXISTS Tags (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                	Name VARCHAR(255) NOT NULL UNIQUE,
                    User VARCHAR(255),
                    DateTime INTEGER,
                    FOREIGN KEY(User) REFERENCES Users(Username) ON DELETE SET NULL
                );
                ALTER TABLE Tags ADD COLUMN NSFW INTEGER DEFAULT 0;
                CREATE TABLE IF NOT EXISTS MediasTags (
                    MediaID INTEGER,
                    TagID INTEGER,
                    User VARCHAR(255),
                    DateTime INTEGER,
                    FOREIGN KEY(MediaID) REFERENCES Medias(ID) ON DELETE CASCADE,
                    FOREIGN KEY(TagID) REFERENCES Tags(ID) ON DELETE CASCADE,
                    FOREIGN KEY(User) REFERENCES Users(Username) ON DELETE SET NULL,
                    CONSTRAINT OnlyOneOccurenceOfTagOnFile UNIQUE(MediaID, TagID)
                );
                CREATE TABLE IF NOT EXISTS Users(
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username VARCHAR(255) NOT NULL UNIQUE,
                    Email VARCHAR(255) NOT NULL UNIQUE,
                    PasswordHash BLOB NOT NULL,
                    PasswordSalt BLOB NOT NULL,                  
                    Role VARCHAR(255) NOT NULL,
                    RegisterDateTime INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Comments(
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    Text VARCHAR(255) NOT NULL,
                    User VARCHAR(255),
                    MediaID VARCHAR(255) NOT NULL,
                    Timestamp INTEGER NOT NULL,
                    FOREIGN KEY(User) REFERENCES Users(Username) ON DELETE CASCADE,
                    FOREIGN KEY(MediaID) REFERENCES Medias(Hash) ON DELETE CASCADE
                );
                CREATE TABLE IF NOT EXISTS Tickets(
                    ID VARCHAR(255) PRIMARY KEY,
                    Username VARCHAR(255) NOT NULL,
                    Value BLOB,
                    LastActivity INTEGER,
                    UserAgent VARCHAR(255),
                    IP VARCHAR(255)
                );
                COMMIT;
                PRAGMA foreign_keys=on;";

                try
                {
                    await new SQLiteCommand(createTableQuery, connection).ExecuteNonQueryAsync();
                }
                catch (SQLiteException ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                created = true;
                await connection.CloseAsync();
            }
            return created;
        }
    }
}
