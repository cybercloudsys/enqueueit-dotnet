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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace EnqueueIt.Internal
{
    internal class ProcessingServer
    {
        Server server;
        Workers workers = new Workers();
        StorageHandler storageHandler;

        internal ProcessingServer(Server server)
        {
            this.server = server;
            if (GlobalConfiguration.Current.ServiceProvider != null)
            {
                IHostApplicationLifetime hostLiftime = GlobalConfiguration.Current
                    .ServiceProvider.GetService(typeof(IHostApplicationLifetime)) as IHostApplicationLifetime;
                hostLiftime.ApplicationStopping.Register(() => Stop());
            }
            AppDomain.CurrentDomain.ProcessExit += delegate (object sender, EventArgs e) {
                GlobalConfiguration.Current.Storage.SyncServer(server);
                Stop();
            };
        }

        internal void Start()
        {
            new Thread(() => {
                var gConfig = GlobalConfiguration.Current;
                if (server != null)
                {
                    server.Status = ServerStatus.Running;
                    server.StartedAt = DateTime.UtcNow;
                    server.LastActivity = server.StartedAt;
                    server.HasDataSync = GlobalConfiguration.Current.LongTermStorage != null;
                    gConfig.Storage.SaveServer(server);
                    if (gConfig.Configuration.Applications == null)
                        gConfig.Configuration.Applications = new List<Application>();
                    string appName = AppDomain.CurrentDomain.FriendlyName + ".dll";
                    if (!gConfig.Configuration.Applications.Any(app => app.Name == appName))
                    {
                        gConfig.Configuration.Applications.Add(new Application {
                            Name = appName,
                            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
                            LauncherApp = "dotnet" });
                    }
                    storageHandler = new StorageHandler(server);
                    server.Queues.ForEach(queue => {
                        new Thread(() => DequeueJobs(queue)).Start();
                        new Thread(() => EnqueueScheduled(queue)).Start();
                    });
                    gConfig.Logger.LogInformation("Enqueue It server started.");
                    TimeSpan waitTime = TimeSpan.FromSeconds(gConfig.Configuration.ServerHeartbeatInterval);
                    while (server != null && server.Status == ServerStatus.Running)
                    {
                        try
                        {
                            using (new DistributedLock(server.Id.ToString()))
                            {
                                gConfig.Storage.SyncServer(server);
                                server.LastActivity = DateTime.UtcNow;
                                gConfig.Storage.SaveServer(server);
                            }
                        } catch { }
                        Task.Delay(waitTime).Wait();
                    }
                    if (storageHandler != null)
                        storageHandler.ForceStop();
                }
            }).Start();
        }

        internal void Stop()
        {
            if (server != null && server.Status != ServerStatus.Stopped)
            {
                Servers.Stop(server.Id);
                server.Status = ServerStatus.Stopped;
                GlobalConfiguration.Current.Storage.SyncServer(server);
            }
        }

        private void ExceuteBackgroundJob(Guid bgJobId, Queue queue)
        {
            var distLock = new DistributedLock(bgJobId.ToString());
            BackgroundJob bgJob = GlobalConfiguration.Current.Storage.GetBackgroundJob(bgJobId);
            if (bgJob != null)
            {
                bgJob.Job.Tries += 1;
                bgJob.ProcessedBy = server.Id;
                bgJob.Server = server.Hostname;
                bgJob.StartedAt = DateTime.UtcNow;
                var app = GlobalConfiguration.Current.Configuration.Applications.FirstOrDefault(ap => ap.Name == bgJob.Job.AppName);
                if (app == null)
                {
                    var err = new ApplicationNotConfiguredException(bgJob.Job.AppName);
                    bgJob.Error = new JobError(err);
                    bgJob.Completed();
                    GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                    workers.WorkerDisposed(queue.Name);
                    GlobalConfiguration.Current.Logger.LogError(err, $"Background job {bgJobId} failed to start");
                    return;
                }
                bgJob.Status = JobStatus.Processing;
                GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                distLock.Dispose();
                GlobalConfiguration.Current.Logger.LogDebug($"Backgorund job {bgJobId} is start processing...");
                JobMonitoring jobMonitoring = new JobMonitoring(bgJobId, queue, workers);
                if (bgJob.Job.Type == JobType.Thread)
                    jobMonitoring.MonitorJobThread(ExecuteThread(bgJobId));
                else
                    jobMonitoring.MonitorMicroservice(ExceuteMicroservice(bgJob, app));
            }
            else
                distLock.Dispose();
        }

        private void DequeueJobs(Queue queue)
        {
            int interval = 10;
            workers.AddQueue(queue.Name);
            while (server != null && server.Status == ServerStatus.Running)
            {
                if (workers.TotalWorkers() < server.WorkersCount && workers.QueueWorkers(queue.Name) < queue.WorkersCount)
                {
                    var jobId = GlobalConfiguration.Current.Storage.Dequeue(queue.Name);
                    if (jobId.HasValue)
                    {
                        workers.WorkerStarted(queue.Name);
                        new Thread(() => ExceuteBackgroundJob(jobId.Value, queue)).Start();
                        interval = 10;
                    }
                    else
                    {
                        Task.Delay(interval).Wait();
                        if (interval < 1000)
                            interval *= 10;
                    }
                }
                else
                    Task.Delay(500).Wait();
            }
        }

        private void EnqueueScheduled(Queue queue)
        {
            DateTime lastCheck = DateTime.UtcNow;
            lastCheck = lastCheck.AddMilliseconds(-lastCheck.Millisecond-1);
            IEnumerable<Job> jobs = null;
            while (server != null && server.Status == ServerStatus.Running)
            {
                DateTime current = DateTime.UtcNow;
                if (current.Second != lastCheck.Second)
                {
                    if (GlobalConfiguration.Current.Storage.ScheduleChanged(server.Id, queue.Name))
                        jobs = GlobalConfiguration.Current.Storage.GetJobs(server.Id, queue.Name);
                    current = current.AddMilliseconds(-current.Millisecond-1).AddSeconds(1);
                    foreach (var job in jobs)
                    {
                        if ((job.StartAt.HasValue && job.StartAt.Value.AddMilliseconds(-job.StartAt.Value.Millisecond-1) <= current && job.Active)
                            || (job.IsRecurring && job.RecurringPattern.IsMatching(current)))
                            BackgroundJobs.EnqueueById(job.Id);
                    }
                    lastCheck = current;
                }
                Task.Delay(500).Wait();
            }
        }

        private Process ExceuteMicroservice(BackgroundJob bgJob, Application app)
        {
            string appName = app.Name;
            if (!string.IsNullOrWhiteSpace(app.BaseDirectory))
                appName = Path.Combine(app.BaseDirectory, appName);
            bool hasLauncherApp = !string.IsNullOrWhiteSpace(app.LauncherApp);
            string appPath = hasLauncherApp ? app.LauncherApp : appName;
            var proc = new ProcessStartInfo(appPath);
            proc.RedirectStandardError = true;
            if (hasLauncherApp)
                proc.Arguments = $"{appName} ";
            else
                proc.Arguments = "";

            string arg = bgJob.Job.Argument;
            if (bgJob.Job.JobArgument != null)
                arg = Serializer.Serialize(bgJob.Job.JobArgument);
            if (!string.IsNullOrWhiteSpace(arg))
                proc.Arguments += $"EnqueueIt.Base64:{Convert.ToBase64String(Encoding.UTF8.GetBytes(arg))}";
            return Process.Start(proc);
        }

        private JobExecution ExecuteThread(Guid bgJobId)
        {
            var jobExec = new JobExecution(bgJobId);
            jobExec.Start(true);
            return jobExec;
        }
    }
}