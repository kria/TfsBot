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
using System.Diagnostics;

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

        public Type[] SubscribedTypes()
        {
            return new Type[1] { typeof(PushNotification) };
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
                    Stopwatch timer = new Stopwatch();
                    timer.Start();

                    PushNotification pushNotification = notificationEventArgs as PushNotification;
                    var repositoryService = requestContext.GetService<TeamFoundationGitRepositoryService>();
                    var commonService = requestContext.GetService<CommonStructureService>();
                    
                    using (TfsGitRepository repository = repositoryService.FindRepositoryById(requestContext, pushNotification.RepositoryId))
                    {
                        string repoName = pushNotification.RepositoryName;
                        string projectName = commonService.GetProject(requestContext, pushNotification.TeamProjectUri).Name;
                        string userName = pushNotification.AuthenticatedUserName.Replace(DOMAIN_PREFIX, "");

                        var lines = new List<string>();
                        
                        string pushText = pushNotification.IsForceRequired(requestContext, repository) ? "FORCE push" : "push";
                        lines.Add(String.Format("{0} by {1} to {2}/{3}", pushText, userName, projectName, repoName));

                        // Display new commits
                        foreach (byte[] commitId in pushNotification.IncludedCommits)
                        {
                            TfsGitCommit gitCommit = (TfsGitCommit)repository.LookupObject(requestContext, commitId);
                            string line = CommitToString(requestContext, gitCommit, "commit", pushNotification);
                            lines.Add(line);
                        }

                        // Display ref updates that are not new commits
                        var refUpdateResultGroups = pushNotification.RefUpdateResults
                            .Where(r => r.Succeeded)
                            .GroupBy(r => r.NewObjectId, (key, items) => new { NewObjectId = key, RefUpdateResults = items }, new ByteArrayComparer());

                        foreach (var refUpdateResultGroup in refUpdateResultGroups)
                        {
                            byte[] newObjectId = refUpdateResultGroup.NewObjectId;
                            bool isIncludedCommit = pushNotification.IncludedCommits.Any(r => r.SequenceEqual(newObjectId));
                            if (isIncludedCommit) continue;

                            string line = null;

                            if (newObjectId.IsZero())
                            {
                                line = String.Format("{0} deleted", RefsToString(refUpdateResultGroup.RefUpdateResults));
                            }
                            else
                            {
                                TfsGitObject gitObject = repository.LookupObject(requestContext, newObjectId);

                                if (gitObject.ObjectType == TfsGitObjectType.Commit)
                                {
                                    line = CommitToString(requestContext, (TfsGitCommit)gitObject, "->", pushNotification, refUpdateResultGroup.RefUpdateResults);
                                }
                                else
                                {
                                    line = String.Format("{0} -> {1} {2}", RefsToString(refUpdateResultGroup.RefUpdateResults), gitObject.ObjectType, newObjectId.ToHexString());
                                }
                            }

                            lines.Add(line);
                        }

                        //Log(lines);
                        Task.Run(() => SendToBot(lines));
                    }

                    timer.Stop();
                    //Log("Time spent in ProcessEvent: " + timer.Elapsed);
                }
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                Log(ex.StackTrace);
            }

            return EventNotificationStatus.ActionPermitted;
        }

        private string RefsToString(IEnumerable<TfsGitRefUpdateResult> refUpdateResults)
        {
            if (refUpdateResults.Count() == 0) return null;
            StringBuilder sb = new StringBuilder();
            foreach(var gitRef in refUpdateResults) {
                sb.Append('[');
                if (gitRef.Name.StartsWith("refs/heads/") && gitRef.OldObjectId.IsZero())
                    sb.Append('+');
                sb.AppendFormat("{0}]", gitRef.Name.Replace("refs/heads/", "").Replace("refs/tags/",""));
            }
            return sb.ToString();
        }

        private string CommitToString(TeamFoundationRequestContext requestContext, TfsGitCommit gitCommit, string action, PushNotification pushNotification, 
            IEnumerable<TfsGitRefUpdateResult> gitRefs = null)
        {
            if (gitRefs == null)
                gitRefs = pushNotification.RefUpdateResults.Where(r => r.Succeeded && r.NewObjectId.SequenceEqual(gitCommit.ObjectId));

            DateTime authorTime = gitCommit.GetLocalAuthorTime(requestContext);
            string authorName = gitCommit.GetAuthor(requestContext);
            string comment = gitCommit.GetComment(requestContext);
            StringBuilder sb = new StringBuilder();
            if (gitRefs.Count() > 0) sb.AppendFormat("{0} ", RefsToString(gitRefs));
            sb.AppendFormat("{0} {1} {2} {3} {4}", action, gitCommit.ObjectId.ToShortHexString(), authorTime.ToString(), authorName,
                comment.Truncate(COMMENT_MAX_LENGTH));

            return sb.ToString();
        }

        private void SendToBot(IEnumerable<string> lines)
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

        private void Log(IEnumerable<string> lines)
        {
            using (StreamWriter sw = File.AppendText(LOGFILE))
            {
                foreach (string line in lines)
                {
                    sw.WriteLine("[{0}] {1}", DateTime.Now, line);
                }
            }
        }
        private void Log(string line)
        {
            Log(new[] { line });
        }

        
    }

        
}
