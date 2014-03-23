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

using Microsoft.TeamFoundation.Framework.Server;
using Microsoft.TeamFoundation.Git.Server;
using Microsoft.TeamFoundation.Integration.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.ServiceModel;
using DevCore.TfsBot.BotServiceContract;

namespace DevCore.TfsBot.TfsSubscriber
{
    class NotificationSubscriber : ISubscriber
    {
        private const string SERVICE_ENDPOINT = "net.pipe://localhost/BotService";
        private const string LOGFILE = @"C:\tfsbot\tfsbot.log";
        private const string DOMAIN_PREFIX = "MYDOMAIN\\";
        private const string TEXT_FORMAT = "\x02{0}\x02";
        private const int COMMENT_MAX_LENGTH = 72;

        public string Name
        {
            get { return "TfsBot handler"; }
        }

        public SubscriberPriority Priority
        {
            get { return SubscriberPriority.Normal; }
        }

        public EventNotificationStatus ProcessEvent(TeamFoundationRequestContext requestContext, NotificationType notificationType,
            object notificationEventArgs, out int statusCode, out string statusMessage, out Microsoft.TeamFoundation.Common.ExceptionPropertyCollection properties)
        {
            statusCode = 0;
            statusMessage = string.Empty;
            properties = null;
           
            try
            {
                if (notificationType == NotificationType.Notification && notificationEventArgs is PushNotification)
                {
                    PushNotification pushNotification = notificationEventArgs as PushNotification;
                    var repositoryService = requestContext.GetService<TeamFoundationGitRepositoryService>();
                    var commonService = requestContext.GetService<CommonStructureService>();
                    
                    using (TfsGitRepository repository = repositoryService.FindRepositoryById(requestContext, pushNotification.RepositoryId))
                    {
                        string repoName = pushNotification.RepositoryName;
                        string projectName = commonService.GetProject(requestContext, pushNotification.TeamProjectUri).Name;
                        string userName = pushNotification.AutenticatedUserName.Replace(DOMAIN_PREFIX, "");

                        var lines = new List<string>();

                        lines.Add(String.Format("push by {0} to {1}/{2}", userName, projectName, repoName));

                        foreach (var item in pushNotification.IncludedCommits)
                        {
                            TfsGitCommit gitCommit = (TfsGitCommit)repository.LookupObject(requestContext, item);
                            string hash = BitConverter.ToString(gitCommit.ObjectId).Replace("-","").ToLower();
                            DateTime authorTime = gitCommit.GetLocalAuthorTime(requestContext);
                            string authorName = gitCommit.GetAuthor(requestContext);
                            string comment = gitCommit.GetComment(requestContext);

                            lines.Add(String.Format("commit {0} {1} {2} {3}", hash.Substring(0, 6), authorTime.ToString(), authorName, 
                                Truncate(comment, COMMENT_MAX_LENGTH)));
                        }
                        //Log(lines);
                        Task.Run(() => SendToBot(lines));
                    }
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Log(ex.StackTrace);
            }

            return EventNotificationStatus.ActionPermitted;
        }

        public Type[] SubscribedTypes()
        {
            return new Type[1] { typeof(PushNotification) };
        }


        private void SendToBot(IEnumerable<String> lines)
        {
            try
            {
                ChannelFactory<IBotService> factory = new ChannelFactory<IBotService>(new NetNamedPipeBinding(),
                    new EndpointAddress(SERVICE_ENDPOINT));
                IBotService service = factory.CreateChannel();
                
                foreach (string line in lines)
                {
                    service.SendMessage(String.Format(TEXT_FORMAT, line));
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Log(ex.StackTrace);
            }
        }

        private string Truncate(string text, int len)
        {
            text = text.TrimEnd(Environment.NewLine.ToCharArray());
            int pos = text.IndexOf('\n');
            if (pos > 0 && pos <= len)
                return text.Substring(0, pos);
            if (text.Length <= len) 
                return text;
            pos = text.LastIndexOf(' ', len);
            if (pos == -1) pos = len;
            
            return text.Substring(0, pos) + "...";
        }

        private void Log(IEnumerable<String> lines)
        {
            using (StreamWriter sw = File.AppendText(LOGFILE))
            {
                foreach (string line in lines)
                {
                    sw.WriteLine(line);
                }
            }
        }
        private void Log(string line)
        {
            Log(new[] { line });
        }
    }
}
