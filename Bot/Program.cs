/*
 * TfsBot - http://github.com/kria/TfsBot
 * 
 * Copyright (C) 2014 Kristian Adrup
 * 
 * This file is part of TfsBot.
 * 
 * TfsBot is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version. See included file COPYING for details.
 */

using ChatSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevCore.TfsBot.BotServiceContract;
using System.Windows.Forms;

namespace DevCore.TfsBot.Bot
{
    class Program : IBotService
    {
        private static IrcClient ircClient;

        static void Main(string[] args)
        {
            var settings = Properties.Settings.Default;

            ircClient = new IrcClient(settings.IrcServer, new IrcUser(settings.IrcNick, settings.IrcNick));
            ircClient.ConnectionComplete += (s, e) => ircClient.JoinChannel(settings.IrcChannel);
            ircClient.ConnectAsync();

            Uri baseAddress = new Uri("net.pipe://localhost");

            using (ServiceHost host = new ServiceHost(typeof(Program), baseAddress))
            {
                host.AddServiceEndpoint(typeof(IBotService), new NetNamedPipeBinding(), "BotService");
                host.Open();
                Console.WriteLine("TfsBot {0} started...", Application.ProductVersion);
                Console.WriteLine("Press <Enter> to stop the bot.");
                Console.ReadLine();

                host.Close();
            }
            ircClient.Quit("Shutdown...");
            
        }

        public void SendMessage(string message)
        {
            //System.Console.WriteLine("Received: " + message);
            var channel = ircClient.Channels.FirstOrDefault(c => c.Name.Equals(Properties.Settings.Default.IrcChannel));
            if (channel != null)
                channel.SendMessage(message);
        }
    }
}
