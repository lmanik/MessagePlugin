using System.Data;
using System.IO;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace MessagePlugin
{
    class TDb
    {
        public static SqlTableEditor SQLEditor;
        public static SqlTableCreator SQLWriter;

        public static void InitTshockDB()
        {
            SQLEditor = new SqlTableEditor(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            SQLWriter = new SqlTableCreator(TShock.DB, TShock.DB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
        }

    }
}