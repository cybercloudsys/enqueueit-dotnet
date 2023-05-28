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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EnqueueIt.Internal
{
    internal class JobMonitoring
    {
        Guid bgJobId;
        Queue queue;
        Workers workers;

        internal JobMonitoring(Guid bgJobId, Queue queue, Workers workers)
        {
            this.bgJobId = bgJobId;
            this.queue = queue;
            this.workers = workers;
        }

        internal void MonitorJobThread(JobExecution jobExec)
        {
            TimeSpan waitTime = TimeSpan.FromSeconds(GlobalConfiguration.Current.Configuration.JobHeartbeatInterval);
            while (jobExec.Thread.IsAlive)
            {
                using (new DistributedLock(bgJobId.ToString()))
                {
                    var bgJob = GlobalConfiguration.Current.Storage.GetBackgroundJob(bgJobId, false);
                    if (bgJob == null || bgJob.Status == JobStatus.Processed ||
                        bgJob.Status == JobStatus.Failed || bgJob.Status == JobStatus.Canceled
                        || bgJob.Status == JobStatus.Interrupted)
                    {
                        if (jobExec.Thread.IsAlive)
                        {
                            if (jobExec.Stop() && bgJob != null && bgJob.Status == JobStatus.Canceled)
                                bgJob.Status = JobStatus.Interrupted;
                        }
                        if (!jobExec.Thread.IsAlive)
                        {
                            GC.Collect();
                            workers.WorkerDisposed(queue.Name);
                            GlobalConfiguration.Current.Logger.LogDebug($"Background job {bgJobId} is {bgJob.Status}");
                            return;
                        }
                    }
                    if (bgJob != null)
                    {
                        bgJob.LastActivity = DateTime.UtcNow;
                        GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                    }
                }
                Task.Delay(waitTime).Wait();
            }
            JobCompleted(bgJobId, jobExec.Error);
        }

        internal void MonitorMicroservice(Process process)
        {
            JobLog lastLog = new JobLog(), currentLog = null;
            lastLog.Time = DateTime.UtcNow.AddSeconds(-1);
            lastLog.CpuUsage = 0;
            bool lastAdded = false;
            TimeSpan waitTime = TimeSpan.FromSeconds(GlobalConfiguration.Current.Configuration.JobHeartbeatInterval)
                .Subtract(TimeSpan.FromMilliseconds(105));
            while (!process.HasExited)
            {
                try
                {
                    process.Refresh();
                    currentLog = new JobLog();
                    currentLog.Time = DateTime.UtcNow;
                    while (!process.HasExited && currentLog.Time.Millisecond < 500
                        && currentLog.Time.Second > lastLog.Time.Second)
                    {
                        Task.Delay(100).Wait();
                        currentLog.Time = DateTime.UtcNow;
                    }
                    if (!process.HasExited)
                    {
                        currentLog.MemoryUsage = Math.Round(process.WorkingSet64 / 1024.0 / 1024, 2);
                        currentLog.CpuTime = process.TotalProcessorTime.TotalMilliseconds;
                        currentLog.CpuUsage = Math.Round(((currentLog.CpuTime - lastLog.CpuTime) /
                            currentLog.Time.Subtract(lastLog.Time).TotalMilliseconds /
                            Convert.ToDouble(Environment.ProcessorCount)) * 100, 2);
                        lastAdded = lastLog.MemoryUsage != currentLog.MemoryUsage || lastLog.CpuUsage != currentLog.CpuUsage;
                        if (lastAdded)
                            GlobalConfiguration.Current.Storage.AddJobLog(bgJobId, currentLog);
                        lastLog = currentLog;
                        using (new DistributedLock(bgJobId.ToString()))
                        {
                            var bgJob = GlobalConfiguration.Current.Storage.GetBackgroundJob(bgJobId, false);
                            if (bgJob == null || bgJob.Status == JobStatus.Processed ||
                                bgJob.Status == JobStatus.Failed || bgJob.Status == JobStatus.Canceled
                                || bgJob.Status == JobStatus.Interrupted)
                            {
                                if (!process.HasExited)
                                    process.Kill();
                                workers.WorkerDisposed(queue.Name);
                                GlobalConfiguration.Current.Logger.LogDebug($"Microservice {bgJobId} is {bgJob.Status}");
                                return;
                            }
                            bgJob.LastActivity = DateTime.UtcNow;
                            GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                        }
                        Task.Delay(waitTime).Wait();
                    }
                } catch { }
            }
            if (!lastAdded)
            {
                try
                {
                    if (lastLog.MemoryUsage == 0 && process.WorkingSet64 > 0)
                    {
                        lastLog.MemoryUsage = Math.Round(process.WorkingSet64 / 1024.0 / 1024, 2);
                        lastLog.CpuTime = process.TotalProcessorTime.TotalMilliseconds;
                        lastLog.CpuUsage = Math.Round((lastLog.CpuTime / 1000 /
                            Convert.ToDouble(Environment.ProcessorCount)) * 100, 2);
                    }
                    if (lastLog.MemoryUsage > 0)
                        GlobalConfiguration.Current.Storage.AddJobLog(bgJobId, lastLog);
                } catch { }
            }
            JobCompleted(bgJobId, process.ExitCode != 0 ? new JobError(process.StandardError) : null);
        }

        private void JobCompleted(Guid bgJobId, JobError err)
        {
            using (new DistributedLock(bgJobId.ToString()))
            {
                var bgJob = GlobalConfiguration.Current.Storage.GetBackgroundJob(bgJobId);
                if (bgJob != null && bgJob.Status == JobStatus.Processing)
                {
                    bgJob.Error = err;
                    bgJob.Completed();
                    if (bgJob.Status == JobStatus.Failed)
                    {
                        if (GlobalConfiguration.Current.Configuration.Servers != null)
                        {
                            var server = GlobalConfiguration.Current.Configuration.Servers.FirstOrDefault(s => s.Id == bgJob.ProcessedBy);
                            if (server == null)
                                server = GlobalConfiguration.Current.Configuration.Servers.FirstOrDefault(s => s.Id == null);
                            if (server != null && server.Queues != null)
                            {
                                if (queue != null && queue.Retries >= bgJob.Job.Tries)
                                {
                                    bgJob.Job.Active = true;
                                    bgJob.Job.StartAt = DateTime.UtcNow.AddSeconds(queue.RetryInterval);
                                }
                            }
                        }
                    }
                    GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                    if (bgJob.Status == JobStatus.Failed)
                        GlobalConfiguration.Current.Logger.LogError($"{bgJob.Job.Type} {bgJobId} is Failed, reason: {bgJob.Error.Message}");
                    else
                        GlobalConfiguration.Current.Logger.LogDebug($"{bgJob.Job.Type} {bgJobId} is {bgJob.Status}");
                }
                workers.WorkerDisposed(queue.Name);
            }
        }
    }
}