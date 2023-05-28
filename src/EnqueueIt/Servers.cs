// EnqueueIt
// Copyright © 2023 Cyber Cloud Systems LLC

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published
// by the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.

// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Threading;
using EnqueueIt.Internal;
using Microsoft.Extensions.Logging;

namespace EnqueueIt
{
    public class Servers
    {
        public static void Start()
        {
            if (!GlobalConfiguration.Current.JobProcessing && GlobalConfiguration.Current.Configuration != null)
            {
                GlobalConfiguration.Current.Logger.LogInformation("Starting Enqueue It server...");
                Server thisServer = null;
                if (GlobalConfiguration.Current.Configuration.Servers == null)
                    GlobalConfiguration.Current.Configuration.Servers = new List<Server>();
                if (GlobalConfiguration.Current.Configuration.Servers.Count == 0)
                    GlobalConfiguration.Current.Configuration.Servers.Add(new Server());
                foreach (Server server in GlobalConfiguration.Current.Configuration.Servers)
                {
                    if (!string.IsNullOrWhiteSpace(server.Hostname))
                    {
                        if (server.Hostname.ToLower() == Environment.MachineName.ToLower())
                            thisServer = server;
                    }
                    else if (thisServer == null)
                        thisServer = server;
                }
                if (thisServer != null)
                {
                    if (thisServer.Queues == null)
                        thisServer.Queues = new List<Queue>();
                    if (thisServer.Queues.Count == 0)
                        thisServer.Queues.Add(new Queue { Name = "jobs" });
                    thisServer.Id = Guid.NewGuid();
                    thisServer.Hostname = Environment.MachineName;
                    var procServer = new ProcessingServer(thisServer);
                    procServer.Start();
                }
            }
        }

        public static void Stop(Guid serverId)
        {
            var storage = GlobalConfiguration.Current.Storage;
            Server server = null;
            using (new DistributedLock(serverId.ToString()))
            {
                server = storage.GetServer(serverId);
                GlobalConfiguration.Current.Logger.LogInformation("Stopping Enqueue It server...");
                server.Status = ServerStatus.Stopped;
                storage.SaveServer(server);
            }
            foreach (var queue in server.Queues)
            {
                foreach (BackgroundJob job in storage.GetBackgroundJobs(serverId, queue.Name))
                {
                    if (job != null && job.Status == JobStatus.Processing)
                    {
                        using (new DistributedLock(job.Id.ToString()))
                        {
                            var bgJob = storage.GetBackgroundJob(job.Id, false);
                            bgJob.Status = JobStatus.Interrupted;
                            bgJob.CompletedAt = DateTime.UtcNow;
                            storage.SaveBackgroundJob(bgJob);
                        }
                    }
                }
            }
        }
    }
}
