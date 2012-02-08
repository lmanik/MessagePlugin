using System;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Terraria;
using Hooks;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;

namespace MessagePlugin
{
    [APIVersion(1, 11)]
    public class MessagePlugin : TerrariaPlugin
    {
        public static List<MPlayer> Players = new List<MPlayer>();
        public static List<Message> Messages = new List<Message>();
   
        public override string Name
        {
            get { return "Message plugin"; }
        }

        public override string Author
        {
            get { return "Created by Lmanik."; }
        }

        public override string Description
        {
            get { return ""; }
        }

        public override Version Version
        {
            get { return new Version(0, 9, 2); }
        }

        public override void Initialize()
        {
            GameHooks.Update += OnUpdate;
            GameHooks.Initialize += OnInitialize;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            ServerHooks.Chat += OnChat;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Update -= OnUpdate;
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                ServerHooks.Chat -= OnChat;
            }
            base.Dispose(disposing);
        }

        public MessagePlugin(Main game)
            : base(game)
        {
        }

        public void OnInitialize()
        {
            //set tshock db
            TDb.InitTshockDB();

            //init Message Plugin db
            MDb.InitMessageDB();
            
            //set group
            bool msg = false;

            foreach (Group group in TShock.Groups.groups)
            {
                if (group.Name != "superadmin")
                {
                    if (group.HasPermission("msguse"))
                        msg = true;
                }
            }

            List<string> permlist = new List<string>();
            if (!msg)
                permlist.Add("msguse");

            TShock.Groups.AddPermissions("trustedadmin", permlist);

            Commands.ChatCommands.Add(new Command("msguse", Msg, "msg"));
        }


        public void OnUpdate()
        {
        }

        public void OnGreetPlayer(int who, HandledEventArgs e)
        {
            MPlayer player = new MPlayer(who);

            lock (MessagePlugin.Players)
                MessagePlugin.Players.Add(player);

            if (TShock.Players[who].Group.HasPermission("msguse"))
            {
                string name = TShock.Players[who].Name;
                int count = GetUnreadEmailsByName(name);
                TShock.Players[who].SendMessage("You have got " + count + " unread messages.", Color.Yellow);
            }   
        }

        // Remove all players
        public void OnLeave(int ply)
        {
            lock (Players)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    if (Players[i].Index == ply)
                    {
                        Players.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
        }

        // Return unread emails
        public static int GetUnreadEmailsByName(string name)
        {
            if (name != null)
            {
                List<SqlValue> where = new List<SqlValue>();
                where.Add(new SqlValue("mailTo", "'" + name + "'"));
                where.Add(new SqlValue("Read", "'" + "U" + "'"));
                int count = MDb.SQLEditor.ReadColumn("MessagePlugin", "mailTo", where).Count;

                return count;
            }

            return 0;
        }

        // Save message to db
        public static void SendMessage(string to, string from, string text)
        {
            DateTime date = DateTime.Now;

            List<SqlValue> values = new List<SqlValue>();
            values.Add(new SqlValue("mailFrom", "'" + from + "'"));
            values.Add(new SqlValue("mailTo", "'" + to + "'"));
            values.Add(new SqlValue("mailText", "'" + text + "'"));
            values.Add(new SqlValue("Date", "'" + date + "'"));
            values.Add(new SqlValue("Read", "'" + "U" + "'"));
            MDb.SQLEditor.InsertValues("MessagePlugin", values);


            if (TShock.Utils.FindPlayer(to).Count > 0)
            {
                if(MPlayer.GetPlayerByName(to).TSPlayer.Group.HasPermission("msguse"))
                    MPlayer.GetPlayerByName(to).TSPlayer.SendMessage("You have got new message from " + from, Color.Aqua);
            }
            else
            {
                //TODO: notify to email
            }

        }

        //help message
        public static void Help(CommandArgs args)
        {
            args.Player.SendMessage("To send message use /msg <playerName> <message>", Color.Aqua);
            args.Player.SendMessage("lists unread messages by /msg inbox <page number>", Color.Aqua);
            args.Player.SendMessage("lists messages by /msg list <page number>", Color.Aqua);
            args.Player.SendMessage("to read specify message /msg read <id>, id give from list", Color.Aqua);
            args.Player.SendMessage("for delete message use /msg del <id>", Color.Aqua);
        }

        // Run Message command
        public static void Msg(CommandArgs args)
        {
            string cmd = "help";

            if (args.Parameters.Count > 0)
            {
                cmd = args.Parameters[0].ToLower();
            }

            switch (cmd)
            {
                case "help":
                    {
                        //return help
                        Help(args);
                       
                        break;
                    }

                //list of all unread messages
                case "inbox":
                    {
                        // Fetch all unread messages
                        List<SqlValue> where = new List<SqlValue>();
                        where.Add(new SqlValue("mailTo", "'" + MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name + "'"));
                        where.Add(new SqlValue("Read", "'" + "U" + "'"));

                        for (int i = 0; i < MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count; i++)
                        {
                            
                            DateTime date = Convert.ToDateTime(MDb.SQLEditor.ReadColumn("MessagePlugin", "Date", where)[i]);
                            string datum = String.Format("{0:dd.MM.yyyy - HH:mm}", date);

                            //create message list
                            Messages.Add(new Message(
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where)[i].ToString(),
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "mailFrom", where)[i],
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "mailTo", where)[i],
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "mailText", where)[i],
                                datum,
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "Read", where)[i]
                                ));
                        }

                        //How many messages per page
                        const int pagelimit = 5;
                        //How many messages per line
                        const int perline = 1;
                        //Pages start at 0 but are displayed and parsed at 1
                        int page = 0;


                        if (args.Parameters.Count > 1)
                        {
                            if (!int.TryParse(args.Parameters[1], out page) || page < 1)
                            {
                                args.Player.SendMessage(string.Format("Invalid page number ({0})", page), Color.Red);
                                return;
                            }
                            page--; //Substract 1 as pages are parsed starting at 1 and not 0
                        }

                        List<Message> messages = new List<Message>();
                        foreach (Message message in MessagePlugin.Messages)
                        {
                            messages.Add(message);
                        }

                        if (messages.Count == 0)
                        {
                            args.Player.SendMessage("You haven't got unread messages.", Color.Red);
                            return;
                        }

                        //Check if they are trying to access a page that doesn't exist.
                        int pagecount = messages.Count / pagelimit;
                        if (page > pagecount)
                        {
                            args.Player.SendMessage(string.Format("Page number exceeds pages ({0}/{1})", page + 1, pagecount + 1), Color.Red);
                            return;
                        }

                        //Display the current page and the number of pages.
                        args.Player.SendMessage(string.Format("Inbox ({0}/{1}):", page + 1, pagecount + 1), Color.Green);

                        //Add up to pagelimit names to a list
                        var messageslist = new List<string>();
                        for (int i = (page * pagelimit); (i < ((page * pagelimit) + pagelimit)) && i < messages.Count; i++)
                        {
                            messageslist.Add("[" + messages[i].ID + "]" + " " + messages[i].MailFrom + " (" + messages[i].Date + ") [" + messages[i].Read + "]");
                        }

                        //convert the list to an array for joining
                        var lines = messageslist.ToArray();
                        for (int i = 0; i < lines.Length; i += perline)
                        {
                            args.Player.SendMessage(string.Join(", ", lines, i, Math.Min(lines.Length - i, perline)), Color.Yellow);
                        }

                        if (page < pagecount)
                        {
                            args.Player.SendMessage(string.Format("Type /msg inbox {0} for more unread messages.", (page + 2)), Color.Yellow);
                        }

                        //remove all messages
                        int count = Messages.Count;
                        Messages.RemoveRange(0, count);

                        break;
                    }

                //list of all messages
                case "list":
                    {
                        // Fetch all messages
                        List<SqlValue> where = new List<SqlValue>();
                        where.Add(new SqlValue("mailTo", "'" + MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name + "'"));

                        for (int i = 0; i < MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count; i++)
                        {
                            DateTime date = Convert.ToDateTime(MDb.SQLEditor.ReadColumn("MessagePlugin", "Date", where)[i]);
                            string datum = String.Format("{0:dd.MM.yyyy - HH:mm}", date);

                            //create message list
                            Messages.Add(new Message(
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where)[i].ToString(),
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "mailFrom", where)[i],
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "mailTo", where)[i],
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "mailText", where)[i],
                                datum,
                                (string)MDb.SQLEditor.ReadColumn("MessagePlugin", "Read", where)[i]
                                ));
                        }
                  
                        //How many messages per page
                        const int pagelimit = 5;
                        //How many messages per line
                        const int perline = 1;
                        //Pages start at 0 but are displayed and parsed at 1
                        int page = 0;


                        if (args.Parameters.Count > 1)
                        {
                            if (!int.TryParse(args.Parameters[1], out page) || page < 1)
                            {
                                args.Player.SendMessage(string.Format("Invalid page number ({0})", page), Color.Red);
                                return;
                            }
                            page--; //Substract 1 as pages are parsed starting at 1 and not 0
                        }

                        List<Message> messages = new List<Message>();
                        foreach(Message message in MessagePlugin.Messages)
                        {
                            messages.Add(message);
                        }

                        if (messages.Count == 0)
                        {
                            args.Player.SendMessage("You haven't got messages.", Color.Red);
                            return;
                        }

                        //Check if they are trying to access a page that doesn't exist.
                        int pagecount = messages.Count / pagelimit;
                        if (page > pagecount)
                        {
                            args.Player.SendMessage(string.Format("Page number exceeds pages ({0}/{1})", page + 1, pagecount + 1), Color.Red);
                            return;
                        }

                        //Display the current page and the number of pages.
                        args.Player.SendMessage(string.Format("List messages ({0}/{1}):", page + 1, pagecount + 1), Color.Green);

                        //Add up to pagelimit names to a list
                        var messageslist = new List<string>();
                        for (int i = (page * pagelimit); (i < ((page * pagelimit) + pagelimit)) && i < messages.Count; i++)
                        {
                            messageslist.Add("[" + messages[i].ID + "]" + " " + messages[i].MailFrom + " (" + messages[i].Date + ") [" + messages[i].Read + "]");
                        }

                        //convert the list to an array for joining
                        var lines = messageslist.ToArray();
                        for (int i = 0; i < lines.Length; i += perline)
                        {
                            args.Player.SendMessage(string.Join(", ", lines, i, Math.Min(lines.Length - i, perline)), Color.Yellow);
                        }

                        if (page < pagecount)
                        {
                            args.Player.SendMessage(string.Format("Type /msg list {0} for more messages.", (page + 2)), Color.Yellow);
                        }

                        //remove all messages
                        int count = Messages.Count;
                        Messages.RemoveRange(0, count);

                        break;
                    }

                //read a specify message
                case "read":
                    {
                        if (args.Parameters.Count > 1)
                        {
                            List<SqlValue> where = new List<SqlValue>();
                            where.Add(new SqlValue("Id", "'" + args.Parameters[1].ToString() + "'"));
                            where.Add(new SqlValue("mailTo", "'" + MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name + "'"));

                            int count = MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count;
                            if (count > 0)
                            {
                                String id = MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where)[0].ToString();
                                String from = MDb.SQLEditor.ReadColumn("MessagePlugin", "mailFrom", where)[0].ToString();
                                String text = MDb.SQLEditor.ReadColumn("MessagePlugin", "mailText", where)[0].ToString();

                                DateTime date = Convert.ToDateTime(MDb.SQLEditor.ReadColumn("MessagePlugin", "Date", where)[0]);
                                string datum = String.Format("{0:dd.MM.yyyy - HH:mm}", date);

                                args.Player.SendMessage(id + ") On " + datum + ", " + from + " wrote:", Color.Aqua);
                                args.Player.SendMessage(text, Color.White);

                                //set message to read
                                List<SqlValue> values = new List<SqlValue>();
                                values.Add(new SqlValue("Read", "'" + "R" + "'"));
                                MDb.SQLEditor.UpdateValues("MessagePlugin", values, where);
                            }
                            else
                            {
                                args.Player.SendMessage("Messages with this id \"" + args.Parameters[1].ToString() + "\" is not exist.", Color.Red);
                            }
                        }
                        else
                        {
                            args.Player.SendMessage("You must set Email ID", Color.Red);
                        }

                        break;
                    }

                //delete specify messages
                case "del":
                    {
                        if (args.Parameters.Count > 1)
                        {
                            //switch args [id, unread, read, all]
                            switch (args.Parameters[1].ToString())
                            {
                                case "all":
                                    {
                                        // Fetch all messages
                                        List<SqlValue> where = new List<SqlValue>();
                                        where.Add(new SqlValue("mailTo", "'" + MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name + "'"));

                                        if (MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count > 0)
                                        {
                                            for (int i = 0; i < MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count; i++)
                                            {
                                                where.Add(new SqlValue("Id", "'" + MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where)[i] + "'"));
                                                MDb.SQLWriter.DeleteRow("MessagePlugin", where);
                                            }

                                            args.Player.SendMessage("All messages were deleted.", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendMessage("You haven't god any messages.", Color.Red);
                                        }
                                        

                                        break;
                                    }
                                case "read":
                                    {
                                        // Fetch all read messages
                                        List<SqlValue> where = new List<SqlValue>();
                                        where.Add(new SqlValue("mailTo", "'" + MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name + "'"));
                                        where.Add(new SqlValue("Read", "'" + "R" + "'"));

                                        if (MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count > 0)
                                        {
                                            for (int i = 0; i < MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count; i++)
                                            {
                                                where.Add(new SqlValue("Id", "'" + MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where)[i] + "'"));
                                                MDb.SQLWriter.DeleteRow("MessagePlugin", where);
                                            }

                                            args.Player.SendMessage("All read messages were deleted", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendMessage("You haven't got any read messages.", Color.Red);
                                        }

                                        break;
                                    }

                                case "unread":
                                    {
                                        // Fetch all unread messages
                                        List<SqlValue> where = new List<SqlValue>();
                                        where.Add(new SqlValue("mailTo", "'" + MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name + "'"));
                                        where.Add(new SqlValue("Read", "'" + "U" + "'"));

                                        if (MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count > 0)
                                        {
                                            for (int i = 0; i < MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count; i++)
                                            {
                                                where.Add(new SqlValue("Id", "'" + MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where)[i] + "'"));
                                                MDb.SQLWriter.DeleteRow("MessagePlugin", where);
                                            }

                                            args.Player.SendMessage("All unread messages were deleted.", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendMessage("You haven't god any unread messages.", Color.Red);
                                        }

                                        break;
                                    }

                                default:
                                    {
                                        List<SqlValue> where = new List<SqlValue>();
                                        where.Add(new SqlValue("Id", "'" + args.Parameters[1].ToString() + "'"));
                                        where.Add(new SqlValue("mailTo", "'" + MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name + "'"));

                                        int count = MDb.SQLEditor.ReadColumn("MessagePlugin", "Id", where).Count;
                                        if (count > 0)
                                        {
                                            MDb.SQLWriter.DeleteRow("MessagePlugin", where);
                                            args.Player.SendMessage("Message with id \"" + args.Parameters[1].ToString() + "\" was deleted.", Color.Red);
                                        }
                                        else
                                        {
                                            args.Player.SendMessage("Message with id \"" + args.Parameters[1].ToString() + "\" is not exist.", Color.Red);
                                        }

                                        break;
                                    }
                            }  
                        }
                        else
                        {
                            args.Player.SendMessage("You must set second parameter [id, all, unread, read]", Color.Red);
                        }
                        break;
                    }

                //send message
                default:
                    {

                        if (args.Parameters.Count > 1)
                        {
                            int player = MPlayer.GetPlayerInDb(args.Parameters[0].ToString());

                            if (player > 0)
                            { 
                                string mailTo = args.Parameters[0].ToString();
                                SendMessage(mailTo, MPlayer.GetPlayerById(args.Player.Index).TSPlayer.Name, args.Parameters[1]);

                                args.Player.SendMessage("You send message to " + mailTo, Color.Green);
                            }
                            else
                            {
                                args.Player.SendMessage("Player " + args.Parameters[0] + " is not exist.", Color.Red);
                            }

                        }
                        else
                        {
                            //return help
                            Help(args);
                        }

                        break;
                    }
            }
        }
    }
}