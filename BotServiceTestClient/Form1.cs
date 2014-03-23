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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevCore.TfsBot.BotServiceContract;

namespace DevCore.TfsBot.BotServiceTestClient
{
    public partial class Form1 : Form
    {
        IBotService service = null;

        public Form1()
        {
            InitializeComponent();

            ChannelFactory<IBotService> factory = 
                new ChannelFactory<IBotService>(new NetNamedPipeBinding(), 
                    new EndpointAddress("net.pipe://localhost/BotService"));

            service = factory.CreateChannel();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string message = "\x02" + textBox1.Text + "\x02";
            service.SendMessage(message);
            textBox1.Text = String.Empty;
        }
    }
}
