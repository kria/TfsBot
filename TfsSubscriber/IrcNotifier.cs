/*
 * TfsBot - http://github.com/kria/TfsBot
 * 
 * Copyright (C) 2015 Kristian Adrup
 * 
 * This file is part of TfsBot.
 * 
 * TfsBot is free software: you can redistribute it and/or modify it
 * under the terms of the GNU General Public License as published by the
 * Free Software Foundation, either version 3 of the License, or (at your
 * option) any later version. See included file COPYING for details.
 */

using DevCore.TfsBot.BotServiceContract;
using DevCore.TfsNotificationRelay;
using DevCore.TfsNotificationRelay.Configuration;
using DevCore.TfsNotificationRelay.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace DevCore.TfsBot.TfsSubscriber
{
    public class IrcNotifier : INotifier
    {
        public void Notify(INotification notification, BotElement bot)
        {
            string servicEndpoint = bot.GetSetting("serviceEndpoint", "net.pipe://localhost/BotService");

            ChannelFactory<IBotService> factory = new ChannelFactory<IBotService>(new NetNamedPipeBinding(),
                    new EndpointAddress(servicEndpoint));
            IBotService service = factory.CreateChannel();

            foreach (string line in notification.ToMessage(bot))
            {
                service.SendMessage(line);
            }
        }

    }
}
