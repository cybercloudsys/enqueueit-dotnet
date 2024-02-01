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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EnqueueIt.Internal
{
    internal class StorageHandler
    {
        Server server;
        CancellationTokenSource cts;
        internal StorageHandler(Server server)
        {
            this.server = server;
            cts = new CancellationTokenSource();
            Process(CleanStorage, GlobalConfiguration.Current.Configuration.CleanStorageInterval);
            Process(DeleteExpiredJobs, 3600);
            if (GlobalConfiguration.Current.LongTermStorage != null)
                Process(SyncJobs, GlobalConfiguration.Current.Configuration.StorageSyncInterval);
        }

        internal void ForceStop()
        {
            cts.Cancel();
        }

        private void Process(Action action, int seconds)
        {
            new Thread(() => {
                TimeSpan waitTime = TimeSpan.FromSeconds(seconds);
                while (server.Status == ServerStatus.Running)
                {
                    action.Invoke();
                    try
                    {
                        Task.Delay(waitTime, cts.Token).Wait();
                    } catch { }
                }
            }).Start();
        }

        private void SyncJobs()
        {
            using (var distLock = new DistributedLock("SyncJobs", false))
            {
                if (distLock.TryEnter())
                {
                    List<BackgroundJob> bgJobs = new List<BackgroundJob>();
                    LoadLogs(bgJobs, GlobalConfiguration.Current.Storage.GetBackgroundJobs(JobStatus.Processed, null, 0, -1));
                    LoadLogs(bgJobs, GlobalConfiguration.Current.Storage.GetBackgroundJobs(JobStatus.Failed, null, 0, -1));
                    int batchSize = GlobalConfiguration.Current.Configuration.StorageSyncBatchSize;
                    if (bgJobs.Count > batchSize)
                    {
                        for (int i = 0; i < Math.Ceiling(bgJobs.Count / (double)batchSize); i++)
                        {
                            int startIndex = i * batchSize;
                            MoveJobs(bgJobs.GetRange(startIndex, Math.Min(batchSize, bgJobs.Count - startIndex)));
                            if (server.Status != ServerStatus.Running)
                                break;
                        }
                    }
                    else if (bgJobs.Count > 0)
                        MoveJobs(bgJobs);
                }
            }
        }

        private void MoveJobs(List<BackgroundJob> bgJobs)
        {
            GlobalConfiguration.Current.Logger.LogDebug("Start syncing a batch of {0} ...", bgJobs.Count);
            GlobalConfiguration.Current.LongTermStorage.SaveBackgroundJobs(bgJobs);
            GlobalConfiguration.Current.Logger.LogDebug("{0} jobs saved in sql db.", bgJobs.Count);
            GlobalConfiguration.Current.Storage.DeleteBackgroundJobs(bgJobs);
            GlobalConfiguration.Current.Logger.LogDebug("The batch of {0} is synced.", bgJobs.Count);
        }

        private void CleanStorage()
        {
            using (var distLock = new DistributedLock("CleanStorage", false))
            {
                if (distLock.TryEnter())
                {
                    DeleteInactiveLocks();
                    StopInactiveJobs();
                }
            }
       }

        private void DeleteExpiredJobs()
        {
            using (var distLock = new DistributedLock("DeleteExpiredJobs", false))
            {
                if (distLock.TryEnter())
                {
                    if (GlobalConfiguration.Current.LongTermStorage != null)
                        GlobalConfiguration.Current.LongTermStorage.DeleteExpired();
                    else
                        GlobalConfiguration.Current.Storage.DeleteExpired();
                }
            }
        }

        private void LoadLogs(List<BackgroundJob> bgJobs, IEnumerable<BackgroundJob> jobs)
        {
            foreach (var bgJob in jobs)
            {
                bgJob.JobLogs = GlobalConfiguration.Current.Storage.GetJobLogs(bgJob.Id);
                bgJobs.Add(bgJob);
            }
        }

        private void StopInactiveJobs()
        {
            foreach (var bgJob in GlobalConfiguration.Current.Storage.GetBackgroundJobs(JobStatus.Processing))
            {
                if ((bgJob.LastActivity.HasValue || bgJob.StartedAt.HasValue) &&
                    (DateTime.UtcNow - (bgJob.LastActivity ?? bgJob.StartedAt.Value))
                    .TotalSeconds > GlobalConfiguration.Current.Configuration.InactiveJobTimeout)
                {
                    bgJob.Status = JobStatus.Interrupted;
                    GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                }
            }
        }

        private void DeleteInactiveLocks()
        {
            foreach (var distLock in GlobalConfiguration.Current.Storage.GetAllDistributedLocks())
            {
                if ((DateTime.UtcNow - distLock.LastActivity).TotalSeconds > GlobalConfiguration
                    .Current.Configuration.InactiveLockTimeout)
                    GlobalConfiguration.Current.Storage.DeleteDistributedLock(distLock.Key, distLock.Id);
            }
        }
    }
}