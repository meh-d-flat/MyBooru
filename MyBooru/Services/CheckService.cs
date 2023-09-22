using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class CheckService : Contracts.ICheckService
    {
        readonly IConfiguration config;
        private readonly IQueryService queryService;

        public CheckService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
        }

        public async Task<int> MediasCountAsync()
        {
            return await queryService.QueryTheDb<int>(async x =>
             {
                 return Convert.ToInt32(await x.ExecuteScalarAsync());
             }, "SELECT COUNT(*) FROM Medias");
        }

        public async Task<bool> CheckMediaExistsAsync(string id)
        {
            return await queryService.QueryTheDb<bool>(async x => 
            {
                x.Parameters.AddNew("@p", id, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteScalarAsync());
            }, "SELECT COUNT(*) FROM Medias WHERE Hash = @p");
        }

        public async Task<bool> DBSetupAsync()
        {
            await CheckDbFileExists();
            return await CreateDbTablesAsync();
        }

        async Task CheckDbFileExists()
        {
            if (File.Exists("db.sqlite3"))
                return;

            await Task.Run(() => SQLiteConnection.CreateFile("db.sqlite3"));
        }

        //SELECT COUNT(name) FROM sqlite_master WHERE type='table' AND name='';
        async Task<bool> CreateDbTablesAsync()
        {
            bool created = false;
            using (SQLiteConnection connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value))
            {
                await connection.OpenAsync();//Size INTEGER NOT NULL, Binary BLOB,
                string createTableQuery =
                @"CREATE TABLE IF NOT EXISTS Medias (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
            	    Name VARCHAR(255),
            	    Hash VARCHAR(255) NOT NULL,
            	    Type VARCHAR(255) NOT NULL,
                    Path VARCHAR(255),
                    Thumb VARCHAR(255),
                    CONSTRAINT HashAlreadyExists UNIQUE(Hash)
                );
                CREATE TABLE IF NOT EXISTS Tags (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                	Name VARCHAR(255) NOT NULL UNIQUE
                ); 
                CREATE TABLE IF NOT EXISTS MediasTags (
                    MediaID INTEGER,
                    TagID INTEGER,
                    FOREIGN KEY(MediaID) REFERENCES Medias(ID),
                    FOREIGN KEY(TagID) REFERENCES Tags(ID),
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
                CREATE TABLE IF NOT EXISTS Tickets(
                    ID VARCHAR(255) PRIMARY KEY,
                    Username VARCHAR(255) NOT NULL,
                    Value BLOB,
                    LastActivity INTEGER,
                    UserAgent VARCHAR(255),
                    IP VARCHAR(255)
                );";

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
