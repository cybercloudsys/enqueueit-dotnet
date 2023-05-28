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
using System.Reflection;

namespace EnqueueIt
{
    public interface IStorage
    {
        int GetTotalActiveServers();
        IEnumerable<Server> GetServers();
        Server GetServer(Guid serverId);
        void SyncServer(Server server);
        void SaveServer(Server server);
        IEnumerable<string> GetQueues();
        BackgroundJob GetLatestBackgroundJob(Guid jobId);
        Dictionary<string, Dictionary<JobStatus, int>> GetDailyStatus(DateTime time);
        int GetTotalFailedJobs();
        int GetTotalEnqueuedJobs();
        long GetBackgroundJobsCount(JobStatus status, Guid? jobId = null);
        IEnumerable<BackgroundJob> GetBackgroundJobs(JobStatus status, Guid? jobId = null, long start = 0, long end = 19);
        long GetQueueJobsCount(string queue);
        IEnumerable<BackgroundJob> GetQueueJobs(string queue, long start = 0, long end = 19);
        bool HasRunningJobs(Guid serverId);
        long GetScheduledJobsCount(string status);
        IEnumerable<Job> GetScheduledJobs(string status, long start = 0, long end = 19);
        IEnumerable<Job> GetJobs(Guid serverId, string queue);
        bool ScheduleChanged(Guid serverId, string queue);
        IEnumerable<BackgroundJob> GetBackgroundJobs(Guid serverId, string queue);
        List<JobLog> GetJobLogs(Guid backgroundJobId);
        BackgroundJob GetBackgroundJob(Guid backgroundJobId, bool includeDetails = true);
        Job GetJob(Guid jobId, bool loadLatest = false);
        void SaveBackgroundJob(BackgroundJob backgroundJob);
        Guid? Dequeue(string queue);
        void SaveJob(Job job, bool forceUpdate = false);
        void DeleteBackgroundJob(Guid backgroundJobId);
        void DeleteJob(Guid jobId, bool deleteBackgroundJobs = false);
        void DeleteBackgroundJobs(List<BackgroundJob> backgroundJobs);
        void JobEnqueued(Guid jobId, string queue);
        void EnqueueAfter(Guid jobId, Guid backgroundJobId);
        void AddJobLog(Guid backgorundJobId, JobLog log);
        void SaveDistributedLock(DistributedLockItem distLock);
        bool IsDistributedLockEntered(string key, string id);
        void DeleteDistributedLock(string key, string id);
        long DistributedLocksCount(string key);
        List<DistributedLockItem> GetAllDistributedLocks();
        Dictionary<string, string> GetAllKeys();
        void DeleteExpired();
        void DeleteAll();
        Assembly GetStorageAssembly();
    }
}
