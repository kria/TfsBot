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
        private const int MAX_LINES = 10;

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

                        var refNames = new Dictionary<byte[], List<string>>(new ByteArrayComparer());
                        var oldCommits = new HashSet<byte[]>(new ByteArrayComparer());
                        var unknowns = new List<RefUpdateResultGroup>();

                        // Associate refs (branch, ligtweight and annotated tag) with corresponding commit
                        var refUpdateResultGroups = pushNotification.RefUpdateResults
                            .Where(r => r.Succeeded)
                            .GroupBy(r => r.NewObjectId, (key, items) => new RefUpdateResultGroup(key, items), new ByteArrayComparer());

                        foreach (var refUpdateResultGroup in refUpdateResultGroups)
                        {
                            byte[] newObjectId = refUpdateResultGroup.NewObjectId;
                            byte[] commitId = null;
 
                            if (newObjectId.IsZero())
                            {
                                commitId = newObjectId;
                            }
                            else
                            {
                                TfsGitObject gitObject = repository.LookupObject(requestContext, newObjectId);

                                if (gitObject.ObjectType == TfsGitObjectType.Commit)
                                {
                                    commitId = newObjectId;
                                }
                                else if (gitObject.ObjectType == TfsGitObjectType.Tag)
                                {
                                    var tag = (TfsGitTag)gitObject;
                                    var commit = tag.TryResolveToCommit(requestContext);
                                    if (commit != null)
                                    {
                                        commitId = commit.ObjectId;
                                    }
                                }
                            }

                            if (commitId != null)
                            {
                                List<string> names;
                                if (!refNames.TryGetValue(commitId, out names))
                                {
                                    names = new List<string>();
                                    refNames.Add(commitId, names);
                                }
                                names.AddRange(RefsToStrings(refUpdateResultGroup.RefUpdateResults));

                                if (commitId.IsZero() || !pushNotification.IncludedCommits.Any(r => r.SequenceEqual(commitId)))
                                {
                                    oldCommits.Add(commitId);
                                }
                            }
                            else 
                            {
                                unknowns.Add(refUpdateResultGroup);
                            }
                            
                        }

                        // Display new commits with refs
                        foreach (byte[] commitId in pushNotification.IncludedCommits)
                        {
                            TfsGitCommit gitCommit = (TfsGitCommit)repository.LookupObject(requestContext, commitId);
                            string line = CommitToString(requestContext, gitCommit, "commit", pushNotification, refNames);
                            lines.Add(line);
                        }

                        // Display updated refs to old commits
                        foreach (byte[] commitId in oldCommits)
                        {
                            string line = null;

                            if (commitId.IsZero())
                            {
                                line = String.Format("{0} deleted", String.Join("", refNames[commitId]));
                            }
                            else
                            {
                                TfsGitCommit gitCommit = (TfsGitCommit)repository.LookupObject(requestContext, commitId);
                                line = CommitToString(requestContext, gitCommit, "->", pushNotification, refNames);
                            }
                            lines.Add(line);
                        }

                        // Display "unknown" refs
                        foreach (var refUpdateResultGroup in unknowns)
                        {
                            byte[] newObjectId = refUpdateResultGroup.NewObjectId;
                            TfsGitObject gitObject = repository.LookupObject(requestContext, newObjectId);
                            string line = String.Format("{0} -> {1} {2}", RefsToString(refUpdateResultGroup.RefUpdateResults), gitObject.ObjectType, newObjectId.ToHexString());

                            lines.Add(line);
                        }

                        //Log(lines);

                        List<string> sendLines = lines;
                        if (lines.Count > MAX_LINES)
                        {
                            sendLines = lines.Take(MAX_LINES).ToList();
                            sendLines.Add(String.Format("{0} more line(s) suppressed.", lines.Count - MAX_LINES));
                        }

                        Task.Run(() => SendToBot(sendLines));
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
            return String.Join("", RefsToStrings(refUpdateResults));
        }

        private string[] RefsToStrings(IEnumerable<TfsGitRefUpdateResult> refUpdateResults)
        {
            if (refUpdateResults.Count() == 0) return null;
            var refStrings = new List<string>();
            foreach (var gitRef in refUpdateResults)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append('[');
                if (gitRef.Name.StartsWith("refs/heads/") && gitRef.OldObjectId.IsZero())
                    sb.Append('+');
                sb.AppendFormat("{0}]", gitRef.Name.Replace("refs/heads/", "").Replace("refs/tags/", ""));
                refStrings.Add(sb.ToString());
            }
            return refStrings.ToArray();
        }

        private string CommitToString(TeamFoundationRequestContext requestContext, TfsGitCommit gitCommit, string action, PushNotification pushNotification, 
            Dictionary<byte[], List<string>> refNames)
        {
            DateTime authorTime = gitCommit.GetLocalAuthorTime(requestContext);
            string authorName = gitCommit.GetAuthor(requestContext);
            string comment = gitCommit.GetComment(requestContext);
            StringBuilder sb = new StringBuilder();
            List<string> names = null;
            if (refNames.TryGetValue(gitCommit.ObjectId, out names)) sb.AppendFormat("{0} ", String.Join("", names));
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

    class RefUpdateResultGroup
    {
        public RefUpdateResultGroup(byte[] newObjectId, IEnumerable<TfsGitRefUpdateResult> refUpdateResults)
        {
            this.NewObjectId = newObjectId;
            this.RefUpdateResults = refUpdateResults;
        }
        public byte[] NewObjectId { get; set; }
        public IEnumerable<TfsGitRefUpdateResult> RefUpdateResults { get; set; }

    }

}
