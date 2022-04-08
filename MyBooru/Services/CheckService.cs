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

        public bool DBSetup()
        {
            return (!DbFileExists());
        }

        bool DbFileExists()
        {
            bool exists = false;
            if (File.Exists("db.sqlite3"))
                return true;
            else
                SQLiteConnection.CreateFile("db.sqlite3");

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

                connection.Close();
            }


            return exists;
        }
    }
}
