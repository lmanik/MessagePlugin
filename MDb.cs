using System.Data;
using System.IO;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace MessagePlugin
{
    class MDb
    {
        public static string terraDB = Path.Combine(TShock.SavePath, "message_plugin.sqlite");
        public static IDbConnection DB;
        public static SqlTableEditor SQLEditor;
        public static SqlTableCreator SQLWriter;

        public static void InitMessageDB()
        {
            string sql = Path.Combine(terraDB);
            if (!File.Exists(terraDB))
            {
                SqliteConnection.CreateFile(terraDB);
            }

            DB = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
            SQLWriter = new SqlTableCreator(DB, new SqliteQueryCreator());
            SQLEditor = new SqlTableEditor(DB, new SqliteQueryCreator());

            var table = new SqlTable("MessagePlugin",
                new SqlColumn("Id", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("mailFrom", MySqlDbType.Text),
                new SqlColumn("mailTo", MySqlDbType.Text),
                new SqlColumn("mailText", MySqlDbType.Text),
                new SqlColumn("Date", MySqlDbType.Text),
                new SqlColumn("Read", MySqlDbType.Text)
                );

            SQLWriter.EnsureExists(table);
        }

    }
}