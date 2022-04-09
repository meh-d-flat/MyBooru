using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Data.SQLite;

namespace MyBooru.Services
{
    public class CheckService : Contracts.ICheckService
    {
        readonly IConfiguration config;

        public CheckService(IConfiguration configuration)
        {
            config = configuration;
        }

        public bool CheckMediaExists(string id)
        {
            bool exists = false;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string checkExistsQuery = String.Format("SELECT COUNT(*) FROM Medias WHERE Hash = '{0}'", id);

            using (SQLiteCommand checkExists = new SQLiteCommand(checkExistsQuery, connection))
                exists = Convert.ToBoolean(checkExists.ExecuteScalar());

            connection.Close();
            return exists;
        }

        public bool DBSetup()
        {
            CheckDbFileExists();
            return CreateDbTables();
        }

        void CheckDbFileExists()
        {
            if (File.Exists("db.sqlite3"))
                return;

            SQLiteConnection.CreateFile("db.sqlite3");
        }

        //SELECT COUNT(name) FROM sqlite_master WHERE type='table' AND name='';
        bool CreateDbTables()
        {
            bool created = false;
            using (SQLiteConnection connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value))
            {
                connection.Open();
                string createTableQuery =
                @"CREATE TABLE IF NOT EXISTS Medias (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
            	    Name VARCHAR(255),
            	    Hash VARCHAR(255) NOT NULL,
                    Size INTEGER NOT NULL,
            	    Type VARCHAR(255) NOT NULL,
                    Binary BLOB,
            	    CONSTRAINT HashAlreadyExists UNIQUE(Hash)
                );
                CREATE TABLE IF NOT EXISTS Tags (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                	Name VARCHAR(255) NOT NULL
                ); 
                CREATE TABLE IF NOT EXISTS MediasTags (
                    MediaID INTEGER,
                    TagID INTEGER,
                    FOREIGN KEY(MediaID) REFERENCES Medias(ID),
                    FOREIGN KEY(TagID) REFERENCES Tags(ID),
                    CONSTRAINT OnlyOneOccurenceOfTagOnFile UNIQUE(MediaID, TagID)
                );";

                try
                {
                    new SQLiteCommand(createTableQuery, connection).ExecuteNonQuery();
                }
                catch (SQLiteException ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
                created = true;
                connection.Close();
            }
            return created;
        }
    }
}
