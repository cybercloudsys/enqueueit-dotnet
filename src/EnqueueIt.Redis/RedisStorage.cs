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
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using StackExchange.Redis;

namespace EnqueueIt.Redis
{
    public class RedisStorage : IStorage
    {
        static ConnectionMultiplexer redis;
        static IDatabase db;
        
        internal RedisStorage(string connectionString)
        {
            var options = ConfigurationOptions.Parse(connectionString ?? "localhost");
            redis = ConnectionMultiplexer.Connect(options);
            db = redis.GetDatabase();
        }

        public int GetTotalActiveServers()
        {
            return GetServers().Count(s => s.Status == ServerStatus.Running);
        }

        public int GetTotalFailedJobs()
        {
            return (int)db.ListLength(JobStatus.Failed.ToString("D"));
        }

        public int GetTotalEnqueuedJobs()
        {
            long total = 0;
            foreach (var queueKey in redis.GetServers().SelectMany(s => s.Keys(db.Database, "Queue:*")))
                total = db.ListLength(queueKey);
            return (int)total;
        }

        public IEnumerable<Server> GetServers()
        {
            List<Server> servers = new List<Server>();
            foreach (var serverId in db.SetMembers("Servers").Select(id => id))
            {
                var server = GetServer(Guid.Parse(serverId));
                if (server == null || server.LastActivity < DateTime.UtcNow.AddMinutes(-1))
                {
                    db.SetRemove("Servers", serverId);
                    db.KeyDelete(serverId.ToString());
                }
                else
                    servers.Add(server);
            }
            return servers;
        }

        public Server GetServer(Guid serverId)
        {
            var data = db.StringGet(serverId.ToString());
            if (!string.IsNullOrWhiteSpace(data))
                return (Server)JsonSerializer.Deserialize(data, typeof(Server));
            else
                return null;
        }

        public void SyncServer(Server server)
        {
            var data = db.StringGet(server.Id.ToString());
            if (!string.IsNullOrWhiteSpace(data))
            {
                var svr = (Server)JsonSerializer.Deserialize(data, typeof(Server));
                server.Status = svr.Status;
                server.LastActivity = svr.LastActivity;
            }
        }

        public void SaveServer(Server server)
        {
            string serverId = server.Id.ToString();
            db.StringSet(serverId, Serializer.Serialize(server),
                TimeSpan.FromSeconds(GlobalConfiguration.Current.Configuration.InactiveServerTimeout));
            db.SetAdd("Servers", serverId);
        }
        
        public IEnumerable<string> GetQueues()
        {
            HashSet<string> queues = new HashSet<string>();
            foreach (var queue in GetServers().Where(s => s.Status == ServerStatus.Running).SelectMany(s => s.Queues))
                queues.Add(queue.Name);
            foreach (var queueKey in redis.GetServers().SelectMany(s => s.Keys(db.Database, "Queue:*")))
                queues.Add(queueKey.ToString().Substring(6));
            return queues;
        }

        public BackgroundJob GetLatestBackgroundJob(Guid jobId)
        {
            string bgJobId = db.ListGetByIndex($"S:{jobId}", 0);
            if (!string.IsNullOrWhiteSpace(bgJobId))
                return GetBackgroundJob(Guid.Parse(bgJobId), false);
            return null;
        }

        public Dictionary<string, Dictionary<JobStatus, int>> GetDailyStatus(DateTime time)
        {
            time = time.Date;
            var result = new Dictionary<string, Dictionary<JobStatus, int>>();
            DateTime date = DateTime.UtcNow.Date;
            var statuses = new JobStatus[] { JobStatus.Processed, JobStatus.Failed };
            do
            {
                var data = new Dictionary<JobStatus, int>();
                foreach (JobStatus status in statuses)
                    data.Add(status, 0);
                result.Add(date.ToString("yyyy-MM-dd"), data);
                date = date.AddDays(-1);
            }
            while (date >= time);

            foreach (JobStatus status in statuses)
            {
                string key = status.ToString("D");
                int i = 0;
                while (true) {
                    var bgJobId = db.ListGetByIndex(key, i);
                    if (!string.IsNullOrWhiteSpace(bgJobId))
                    {
                        var bgJob = GetBackgroundJob(Guid.Parse(bgJobId));
                        if (bgJob.CompletedAt.Value.Date >= time)
                        {
                            string dateKey = bgJob.CompletedAt.Value.ToString("yyyy-MM-dd");
                            if (!result.ContainsKey(dateKey))
                                result.Add(dateKey, new Dictionary<JobStatus, int>());
                            result[dateKey][status] = result[dateKey][status] + 1;
                        }
                        else
                            break;
                    }
                    else
                        break;
                    i++;
                }
            }
            return result;
        }

        public long GetBackgroundJobsCount(JobStatus status, Guid? jobId = null)
        {
            return db.ListLength(jobId.HasValue ? $"{status.ToString("D")}:{jobId}" : status.ToString("D"));
        }

        public IEnumerable<BackgroundJob> GetBackgroundJobs(JobStatus status,
            Guid? jobId = null, long start = 0, long end = 19)
        {
            List<BackgroundJob> jobs = new List<BackgroundJob>();
            string key = jobId.HasValue ? $"{status.ToString("D")}:{jobId}" : status.ToString("D");
            foreach (var bgJobId in db.ListRange(key, start, end))
            {
                var bgJob = GetBackgroundJob(Guid.Parse(bgJobId));
                if (bgJob != null)
                    jobs.Add(bgJob);
                else
                    db.ListRemove(key, bgJobId);
            }
            return jobs;
        }

        public IEnumerable<Job> GetJobs(Guid serverId, string queue)
        {
            List<Job> jobs = new List<Job>();
            foreach (var jobId in db.ListRange($"QueueSchedule:{queue}"))
                jobs.Add(GetJob(Guid.Parse(jobId)));
            db.SetAdd($"LatestPulled:{queue}", serverId.ToString());
            return jobs;
        }

        public bool ScheduleChanged(Guid serverId, string queue)
        {
            return !db.SetContains($"LatestPulled:{queue}", serverId.ToString());
        }

        public IEnumerable<BackgroundJob> GetBackgroundJobs(Guid serverId, string queue) {
            List<BackgroundJob> jobs = new List<BackgroundJob>();
            foreach (var jobId in db.ListRange($"{serverId}:{queue}"))
                jobs.Add(GetBackgroundJob(Guid.Parse(jobId)));
            return jobs;
        }

        public long GetQueueJobsCount(string queue)
        {
            return db.ListLength($"Queue:{queue}");
        }

        public IEnumerable<BackgroundJob> GetQueueJobs(string queue, long start = 0, long end = 19)
        {
            List<BackgroundJob> jobs = new List<BackgroundJob>();
            foreach (var bgJobId in db.ListRange($"Queue:{queue}", start, end))
                jobs.Add(GetBackgroundJob(Guid.Parse(bgJobId)));
            return jobs;
        }

        public bool HasRunningJobs(Guid serverId)
        {
            foreach (var key in redis.GetServers().SelectMany(s => s.Keys(db.Database, $"{serverId}:*")))
                if (db.ListLength(key) > 0)
                    return true;
            return false;
        }

        public long GetScheduledJobsCount(string status = "Scheduled")
        {
            return db.ListLength(status);
        }

        public IEnumerable<Job> GetScheduledJobs(string status, long start = 0, long end = 19)
        {
            List<Job> jobs = new List<Job>();
            foreach (var jobId in db.ListRange(status, start, end))
                jobs.Add(GetJob(Guid.Parse(jobId), true));
            return jobs;
        }

        public BackgroundJob GetBackgroundJob(Guid backgroundJobId, bool includeDetails = true)
        {
            string jobData = db.StringGet($"BackgroundJob:{backgroundJobId}");
            if (!string.IsNullOrWhiteSpace(jobData))
            {
                BackgroundJob job = (BackgroundJob)JsonSerializer.Deserialize(jobData, typeof(BackgroundJob));
                if (includeDetails)
                {
                    job.Job = GetJob(job.JobId);
                    if (job.Job == null)
                        return null;
                }
                return job;
            }
            return null;
        }

        public Job GetJob(Guid jobId, bool loadLatest = false)
        {
            string jobData = db.StringGet($"Job:{jobId}");
            if (!string.IsNullOrWhiteSpace(jobData))
            {
                var job = (Job)JsonSerializer.Deserialize(jobData, typeof(Job));
                if (loadLatest)
                {
                    job.BackgroundJobs = new List<BackgroundJob>();
                    var bgJob = GetLatestBackgroundJob(job.Id);
                    if (bgJob != null)
                        job.BackgroundJobs.Add(bgJob);
                    if (job.IsRecurring)
                        job.StartAt = job.RecurringPattern.NextTime();
                }
                return job;
            }
            else
                return null;
        }

        public List<JobLog> GetJobLogs(Guid backgroundJobId)
        {
            List<JobLog> logs = new List<JobLog>();
            foreach (var item in db.ListRange($"Logs:{backgroundJobId}"))
            {
                JobLog log = (JobLog)JsonSerializer.Deserialize(item, typeof(JobLog));
                if (log != null)
                    logs.Add(log);
            }
            return logs;
        }

        public void SaveBackgroundJob(BackgroundJob backgroundJob)
        {
            BackgroundJob oldJob = GetBackgroundJob(backgroundJob.Id);
            if (backgroundJob.Job != null)
                SaveJob(backgroundJob.Job);
            else
                backgroundJob.Job = GetJob(backgroundJob.JobId);
            if (backgroundJob.Job != null)
            {
                db.StringSet($"BackgroundJob:{backgroundJob.Id}", Serializer.Serialize(backgroundJob));
                if (oldJob == null)
                    db.ListLeftPush($"S:{backgroundJob.JobId}", backgroundJob.Id.ToString());
                if (oldJob == null || oldJob.Status != backgroundJob.Status)
                {
                    string bgJobId = backgroundJob.Id.ToString();
                    if (oldJob != null)
                    {
                        db.ListRemove(oldJob.Status.ToString("D"), bgJobId);
                        if(backgroundJob.Job.IsRecurring || backgroundJob.Job.StartAt > DateTime.UtcNow)
                            db.ListRemove($"{oldJob.Status.ToString("D")}:{backgroundJob.JobId}", bgJobId);
                        if (oldJob.Status == JobStatus.Enqueued)
                            db.ListRemove($"Queue:{oldJob.Job.Queue}", oldJob.Id.ToString());
                    }
                    db.ListLeftPush(backgroundJob.Status.ToString("D"), bgJobId);
                    if (backgroundJob.Job.IsRecurring || backgroundJob.Job.StartAt > DateTime.UtcNow)
                        db.ListLeftPush($"{backgroundJob.Status.ToString("D")}:{backgroundJob.JobId}", bgJobId);
                    if (backgroundJob.Status == JobStatus.Enqueued)
                        db.ListRightPush($"Queue:{backgroundJob.Job.Queue}", bgJobId);
                    if (backgroundJob.Status == JobStatus.Processing)
                        db.ListRightPush($"{backgroundJob.ProcessedBy}:{backgroundJob.Job.Queue}", bgJobId);
                    if ((backgroundJob.Status == JobStatus.Processed || backgroundJob.Status == JobStatus.Failed)
                        && db.KeyExists($"After:{backgroundJob.Id}"))
                    {
                        string jobId;
                        do {
                            jobId = db.SetPop($"After:{backgroundJob.Id}");
                            if (!string.IsNullOrWhiteSpace(jobId))
                                EnqueuedAfter(Guid.Parse(jobId), backgroundJob.Id);
                        }
                        while (jobId != null);
                    }
                    if ((backgroundJob.Status == JobStatus.Processed || backgroundJob.Status == JobStatus.Failed
                        || backgroundJob.Status == JobStatus.Interrupted) && backgroundJob.ProcessedBy.HasValue)
                        db.ListRemove($"{backgroundJob.ProcessedBy}:{backgroundJob.Job.Queue}", bgJobId);
                }
            }
        }

        public Guid? Dequeue(string queue)
        {
            string bgJobId = db.ListLeftPop($"Queue:{queue}");
            if (!string.IsNullOrWhiteSpace(bgJobId))
                return Guid.Parse(bgJobId);
            else
                return null;
        }

        public void SaveJob(Job job, bool forceUpdate = false)
        {
            Job oldJob = GetJob(job.Id);
            if (!string.IsNullOrWhiteSpace(job.Name) && oldJob == null)
            {
                string recurringjobId = db.StringGet($"RecurringJob:{job.Name}");
                if (!string.IsNullOrWhiteSpace(recurringjobId))
                {
                    job.Id = Guid.Parse(recurringjobId);
                    oldJob = GetJob(job.Id);
                }
            }
            if (oldJob == null || forceUpdate)
            {
                db.StringSet($"Job:{job.Id}", Serializer.Serialize(job));
                if (job.Active && (oldJob == null || job.StartAt != oldJob.StartAt))
                {
                    string jobId = job.Id.ToString();
                    if (job.IsRecurring || job.StartAt.HasValue)
                    {
                        db.ListLeftPush(job.IsRecurring ? "Recurring" : "Scheduled", jobId);
                        if (job.IsRecurring)
                            db.StringSet($"RecurringJob:{job.Name}", jobId);
                        db.ListLeftPush($"QueueSchedule:{job.Queue}", jobId);
                        db.KeyDelete($"LatestPulled:{job.Queue}");
                    }
                    else if (!string.IsNullOrWhiteSpace(job.AfterBackgroundJobIds))
                        db.ListLeftPush("Waiting", jobId);
                }
            }
        }

        public void DeleteBackgroundJob(Guid backgroundJobId)
        {
            var bgJob = GetBackgroundJob(backgroundJobId);
            if (bgJob != null)
            {
                string bgJobId = backgroundJobId.ToString();
                for (int i = 1; i <= 5; i++)
                {
                    db.ListRemove(i.ToString(), bgJobId);
                    if (bgJob.Job.IsRecurring)
                        db.ListRemove($"{i}:{bgJob.JobId}", bgJobId);
                }
                db.KeyDelete($"BackgroundJob:{backgroundJobId}");
                db.KeyDelete($"Logs:{backgroundJobId}");
                db.ListRemove($"S:{bgJob.JobId}", bgJobId);
                var job = GetJob(bgJob.JobId);
                if (!job.IsRecurring && db.ListLength($"S:{bgJob.JobId}") == 0)
                    DeleteJob(bgJob.JobId);
            }
        }

        public void DeleteJob(Guid jobId, bool deleteBackgroundJobs = false)
        {
            var job = GetJob(jobId);
            string strJobId = jobId.ToString();
            db.KeyDelete($"RecurringJob:{job.Name}");
            db.ListRemove("Scheduled", strJobId);
            db.ListRemove("Recurring", strJobId);
            db.ListRemove($"QueueSchedule:{job.Queue}", strJobId);
            if (job.Active)
                db.KeyDelete($"LatestPulled:{job.Queue}");
            if (deleteBackgroundJobs)
            {
                while (db.ListLength($"S:{jobId}") > 0)
                {
                    string bgJobId = db.ListLeftPop($"S:{jobId}");
                    if (!string.IsNullOrWhiteSpace(bgJobId))
                        DeleteBackgroundJob(Guid.Parse(bgJobId));
                }
            }
            if (db.ListLength($"S:{jobId}") == 0)
            {
                db.KeyDelete($"Job:{jobId}");
                db.KeyDelete($"S:{jobId}");
            }
        }

        public void DeleteBackgroundJobs(List<BackgroundJob> backgroundJobs)
        {
            foreach (var bgJob in backgroundJobs)
            {
                db.KeyDelete($"BackgroundJob:{bgJob.Id}");
                db.ListRemove(((int)bgJob.Status).ToString(), bgJob.Id.ToString());
            }
            System.Threading.Tasks.Task.Run(() => {
                foreach (var bgJob in backgroundJobs)
                {
                    string bgJobId = bgJob.Id.ToString();
                    if (bgJob.Job.IsRecurring)
                        db.ListRemove($"{(int)bgJob.Status}:{bgJob.JobId}", bgJobId);
                    db.KeyDelete($"Logs:{bgJob.Id}");
                    db.ListRemove($"S:{bgJob.JobId}", bgJobId);
                    if (!bgJob.Job.IsRecurring && db.ListLength($"S:{bgJob.JobId}") == 0) 
                    {
                        db.KeyDelete($"Job:{bgJob.JobId}");
                        db.KeyDelete($"S:{bgJob.JobId}");
                        string strJobId = bgJob.JobId.ToString();
                        db.ListRemove("Scheduled", strJobId);
                        db.ListRemove("Recurring", strJobId);
                        db.ListRemove($"QueueSchedule:{bgJob.Job.Queue}", strJobId);
                        if (bgJob.Job.Active)
                            db.KeyDelete($"LatestPulled:{bgJob.Job.Queue}");
                    }
                }
            });
        }

        public void JobEnqueued(Guid jobId, string queue)
        {
            string strJobId = jobId.ToString();
            db.ListRemove($"QueueSchedule:{queue}", strJobId);
            db.ListRemove("Scheduled", strJobId);
            db.KeyDelete($"LatestPulled:{queue}");
        }

        public void EnqueueAfter(Guid jobId, Guid backgroundJobId)
        {
            string strJobId = jobId.ToString();
            db.SetAdd($"After:{backgroundJobId}", strJobId);
            var backgroundJob = GetBackgroundJob(backgroundJobId, false);
            if ((backgroundJob.Status == JobStatus.Processed || backgroundJob.Status == JobStatus.Failed)
                && db.SetRemove($"After:{backgroundJobId}", strJobId))
                EnqueuedAfter(jobId, backgroundJobId);
        }

        private void EnqueuedAfter(Guid jobId, Guid backgroundJobId)
        {
            var job = GetJob(jobId);
            if (job != null)
            {
                if (!job.IsRecurring)
                {
                    job.Active = false;
                    JobEnqueued(jobId, job.Queue);
                }
                BackgroundJob bgJob = new BackgroundJob();
                bgJob.JobId = job.Id;
                bgJob.Id = Guid.NewGuid();
                bgJob.Job = job;
                bgJob.CreatedAt = DateTime.UtcNow;
                bgJob.Status = JobStatus.Enqueued;
                SaveBackgroundJob(bgJob);
                db.ListRemove("Waiting", jobId.ToString());
            }
        }

        public void AddJobLog(Guid backgroundJobId, JobLog log)
        {
            db.ListRightPush($"Logs:{backgroundJobId}", Serializer.Serialize(log));
        }

        public void DeleteExpired()
        {
            DateTime expiryDate = DateTime.UtcNow.AddDays(-GlobalConfiguration.Current.Configuration.StorageExpirationInDays);
            foreach (JobStatus status in new[] { JobStatus.Processed, JobStatus.Failed })
            {
                string key = status.ToString("D");
                long i = db.ListLength(key) - 1;
                while (i >= 0) {
                    var bgJobId = db.ListGetByIndex(key, i);
                    if (!string.IsNullOrWhiteSpace(bgJobId))
                    {
                        var bgJob = GetBackgroundJob(Guid.Parse(bgJobId), false);
                        if (bgJob.CompletedAt.Value.Date < expiryDate)
                            DeleteBackgroundJob(bgJob.Id);
                        else
                            break;
                    }
                    else
                        break;
                    i = db.ListLength(key);
                }
            }
        }

        public void DeleteAll()
        {
            Dictionary<string, string> servers = new Dictionary<string, string>();
            while (db.SetLength("Servers") > 0)
            {
                string serverId = db.SetPop("Servers");
                if (!servers.ContainsKey(serverId))
                    servers.Add(serverId, db.StringGet(serverId));
            }
            foreach (var server in redis.GetServers())
                server.FlushDatabase();
            foreach (var server in servers)
            {
                db.SetAdd("Servers", server.Key);
                db.StringSet(server.Key, server.Value);
            }
        }

        public void SaveDistributedLock(DistributedLockItem distLock)
        {
            if (!db.KeyExists($"DistLock:{distLock.Id}"))
                db.ListRightPush($"DistLockKey:{distLock.Key}", distLock.Id);
            db.StringSet($"DistLock:{distLock.Id}", Serializer.Serialize(distLock));
        }

        public bool IsDistributedLockEntered(string key, string id)
        {
            while (true)
            {
                string firstId = db.ListGetByIndex($"DistLockKey:{key}", 0);
                if (firstId == null)
                    return false;
                string data = db.StringGet($"DistLock:{firstId}");
                if (string.IsNullOrWhiteSpace(data))
                {
                    db.ListRemove($"DistLockKey:{key}", firstId);
                    continue;
                }
                var distLock = (DistributedLockItem)JsonSerializer.Deserialize(data, typeof(DistributedLockItem));
                if ((DateTime.UtcNow - distLock.LastActivity).TotalSeconds >= GlobalConfiguration.Current.Configuration.InactiveLockTimeout)
                    DeleteDistributedLock(distLock.Key, distLock.Id);
                else if (firstId != id)
                    return false;
                else
                    return true;
                Thread.Sleep(10);
            }
        }

        public void DeleteDistributedLock(string key, string id)
        {
            if (db.KeyDelete($"DistLock:{id}"))
                db.ListRemove($"DistLockKey:{key}", id);
        }

        public long DistributedLocksCount(string key)
        {
            return db.ListLength($"DistLockKey:{key}");
        }
        
        public List<DistributedLockItem> GetAllDistributedLocks()
        {
            List<DistributedLockItem> locks = new List<DistributedLockItem>();
            foreach (var key in redis.GetServers().SelectMany(s => s.Keys(db.Database, "DistLock:*")))
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    try
                    {
                        locks.Add(JsonSerializer.Deserialize(db.StringGet(key), typeof(DistributedLockItem)) as DistributedLockItem);
                    }
                    catch { }
                }
            }
            return locks;
        }

        public Dictionary<string, string> GetAllKeys()
        {
            var allkeys = new Dictionary<string, string>();
            foreach (var key in redis.GetServers().SelectMany(s => s.Keys(db.Database, "*")))
            {
                var keyType = db.KeyType(key);
                if (keyType == RedisType.String)
                    allkeys.Add(key, db.StringGet(key));
                else if (keyType == RedisType.List)
                {
                    var len = db.ListLength(key);
                    if (len > 0)
                        allkeys.Add(key, len + db.ListGetByIndex(key, 0).ToString());
                }
            }
            return allkeys;
        }

        public Assembly GetStorageAssembly()
        {
            return Assembly.GetExecutingAssembly();
        }
    }
}