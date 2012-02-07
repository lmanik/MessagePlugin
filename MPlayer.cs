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
    public class MPlayer
    {
        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        //Add other variables here - MAKE SURE YOU DON'T MAKE THEM STATIC

        public MPlayer(int index)
        {
            Index = index;
        }
    }
}