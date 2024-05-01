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
        private readonly string dbFilePath;

        public CheckService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
            dbFilePath = config.GetValue<string>("SQLiteDBPath");
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
            if (File.Exists(dbFilePath))
            {
                await Task.Run(() => File.Copy(dbFilePath, DateTime.Now.GetUnixTime() + "." + dbFilePath));
                return;
            }

            await Task.Run(() => SQLiteConnection.CreateFile(dbFilePath));
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
                DELETE FROM MediasTags WHERE ROWID in (SELECT ROWID FROM pragma_foreign_key_check);
                DELETE FROM Comments WHERE ROWID in (SELECT ROWID FROM pragma_foreign_key_check);
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
                    NSFW INTEGER DEFAULT 0,
                    FOREIGN KEY(User) REFERENCES Users(Username) ON DELETE SET NULL
                );
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
                INSERT OR IGNORE INTO Users (Username, Email, PasswordHash, PasswordSalt, Role, RegisterDateTime) VALUES ('DELETED', 'DELETED', 0, 0, 0, 0);
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
				CREATE TABLE Medias_new (
					ID INTEGER,
					Name VARCHAR(255),
					Hash VARCHAR(255) NOT NULL,
					Type VARCHAR(255) NOT NULL,
					Path VARCHAR(255),
					Thumb VARCHAR(255),
					Uploader VARCHAR(255) DEFAULT 'DELETED',
					Timestamp INTEGER NOT NULL DEFAULT 0,
					FOREIGN KEY(Uploader) REFERENCES Users(Username) ON DELETE SET DEFAULT,
					CONSTRAINT HashAlreadyExists UNIQUE(Hash),
					PRIMARY KEY(ID AUTOINCREMENT)
				);
				CREATE TABLE Tags_new (
					ID INTEGER,
					Name VARCHAR(255) NOT NULL UNIQUE,
					User VARCHAR(255) DEFAULT 'DELETED',
					DateTime INTEGER,
					NSFW INTEGER DEFAULT 0,
					FOREIGN KEY(User) REFERENCES Users(Username) ON DELETE SET DEFAULT,
					PRIMARY KEY(ID AUTOINCREMENT)
				);
				CREATE TABLE MediasTags_new (
					MediaID INTEGER,
					TagID INTEGER,
					User VARCHAR(255) DEFAULT 'DELETED',
					DateTime INTEGER,
					FOREIGN KEY(User) REFERENCES Users(Username) ON DELETE SET DEFAULT,
					FOREIGN KEY(MediaID) REFERENCES Medias(ID) ON DELETE CASCADE,
					FOREIGN KEY(TagID) REFERENCES Tags(ID) ON DELETE CASCADE,
					CONSTRAINT OnlyOneOccurenceOfTagOnFile UNIQUE(MediaID,TagID)
				);
				CREATE TABLE Comments_new (
					ID INTEGER,
					Text VARCHAR(255) NOT NULL,
					User VARCHAR(255) DEFAULT 'DELETED',
					Timestamp INTEGER NOT NULL,
					MediaID	VARCHAR(255) NOT NULL,
					PRIMARY KEY(ID AUTOINCREMENT),
					FOREIGN KEY(User) REFERENCES Users(Username) ON DELETE SET DEFAULT,
					FOREIGN KEY(MediaID) REFERENCES Medias(Hash) ON DELETE CASCADE
				);
				INSERT INTO Comments_new(ID, Text, User, Timestamp, MediaID)
				SELECT c.ID, c.Text, c.User, c.Timestamp, c.MediaID 
				FROM Comments AS c;
				
				INSERT INTO MediasTags_new(MediaID, TagID, User, DateTime)
				SELECT mt.MediaID, mt.TagID , mt.User, mt.DateTime
				FROM MediasTags AS mt;
				
				INSERT INTO Tags_new(ID, Name, User, DateTime, NSFW)
				SELECT t.ID, t.Name , t.User, t.DateTime, t.NSFW
				FROM Tags AS t;
				
				INSERT INTO Medias_new(ID, Name, Hash, Type, Path, Thumb, Uploader, Timestamp)
				SELECT m.ID, m.Name, m.Hash, m.Type, m.Path, m.Thumb, m.Uploader, m.Timestamp
				FROM Medias AS m;
				
				DROP TABLE Comments;
				ALTER TABLE Comments_new RENAME TO Comments;
				
				DROP TABLE MediasTags;
				ALTER TABLE MediasTags_new RENAME TO MediasTags;
				
				DROP TABLE Tags;
				ALTER TABLE Tags_new RENAME TO Tags;
				
				DROP TABLE Medias;
				ALTER TABLE Medias_new RENAME TO Medias;
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
