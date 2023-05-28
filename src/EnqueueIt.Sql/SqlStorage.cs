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
using Microsoft.EntityFrameworkCore;

namespace EnqueueIt.Sql
{
    public abstract class SqlStorage : ILongTermStorage
    {
        protected static string connectionString;
        protected StorageDbContext dbContext;
        protected virtual StorageDbContext  GetDbContext() { return null; }

        public SqlStorage(string connectionString)
        {
            SqlStorage.connectionString = connectionString;
        }

        public int GetTotalFailedJobs()
        {
            var db = GetDbContext();
            lock (db)
            {
                return db.BackgroundJobs.Count(j => j.Status == JobStatus.Failed);
            }
        }

        public long GetBackgroundJobsCount(JobStatus status, string search = null, Guid? jobId = null)
        {
            var db = GetDbContext();
            if (string.IsNullOrWhiteSpace(search))
                search = null;
            else
                search = search.ToLower();
            lock (db)
            {
                string strJobId = jobId?.ToString();
                return db.BackgroundJobs.Count(j => j.Status == status && (strJobId == null || j.JobId == strJobId)
                    && (search == null || j.Id == search || j.JobId == search || j.Job.AppName.Contains(search)
                    || j.Job.Name.ToLower().Contains(search) || j.Job.Argument.ToLower().Contains(search)));
            }
        }

        public IEnumerable<BackgroundJob> GetBackgroundJobs(JobStatus status,
            string search = null, Guid? jobId = null, long start = 0, long end = 19)
        {
            var db = GetDbContext();
            if (string.IsNullOrWhiteSpace(search))
                search = null;
            else
                search = search.ToLower();
            lock (db)
            {
                string strJobId = jobId?.ToString();
                return db.BackgroundJobs.Include(j => j.Job)
                    .Where(j => j.Status == status && (strJobId == null || j.JobId == strJobId)
                        && (search == null || j.Id == search || j.JobId == search || j.Job.AppName.Contains(search)
                        || j.Job.Name.ToLower().Contains(search) || j.Job.Argument.ToLower().Contains(search)))
                    .OrderByDescending(j => j.CompletedAt)
                    .Skip((int)start).Take((int)(end + 1 - start))
                    .Select(job => Jobs.GetBackgroundJob(job)).ToList();
            }
        }

        public BackgroundJob GetLatestBackgroundJob(Guid jobId)
        {
            var db = GetDbContext();
            lock (db)
            {
                return db.BackgroundJobs.Where(j => j.JobId == jobId.ToString()).OrderByDescending(j => j.CompletedAt)
                    .Select(j => Jobs.GetBackgroundJob(j)).FirstOrDefault();
            }
        }

        public Dictionary<string, Dictionary<JobStatus, int>> GetDailyStatus(DateTime time)
        {
            time = time.Date;
            var db = GetDbContext();
            lock (db)
            {
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
                var dbResult = db.BackgroundJobs.Where(bg => bg.Status == JobStatus.Processed || bg.Status == JobStatus.Failed)
                    .GroupBy(bg => new { Date = bg.CompletedAt.Value.Date, Status = bg.Status })
                    .Select(g => new { g.Key.Date, g.Key.Status, Count = g.Count() })
                    .Where(bg => bg.Date >= time);
                if (dbResult != null)
                    foreach (var item in dbResult)
                    {
                        string dateKey = item.Date.ToString("yyyy-MM-dd");
                        if (!result.ContainsKey(dateKey))
                            result.Add(dateKey, new Dictionary<JobStatus, int>());
                        result[dateKey][item.Status] = item.Count;
                    }
                return result;
            }
        }

        public BackgroundJob GetBackgroundJob(Guid backgroundJobId, bool includeDetails = true)
        {
            var db = GetDbContext();
            lock (db)
            {
                var result = db.BackgroundJobs.Where(j => j.Id == backgroundJobId.ToString());
                if (includeDetails)
                {
                    result = result.Include(j => j.Job);
                }
                return Jobs.GetBackgroundJob(result.FirstOrDefault());
            }
        }

        public Job GetJob(Guid jobId, bool loadLatest = false)
        {
            var db = GetDbContext();
            lock (db)
            {
                var jobItem = db.Jobs.FirstOrDefault(j => j.Id == jobId.ToString());
                Job job = null;
                if (jobItem != null)
                    job = Jobs.GetJob(jobItem);
                if (job != null && loadLatest)
                {
                    if (job.IsRecurring)
                        job.StartAt = job.RecurringPattern.NextTime();
                }
                return job;
            }
        }

        public virtual void SaveBackgroundJobs(List<BackgroundJob> backgroundJobs)
        {
        }

        public void DeleteBackgroundJob(Guid backgroundJobId)
        {
            var db = GetDbContext();
            lock (db)
            {
                var bgJob = db.BackgroundJobs.FirstOrDefault(j => j.Id == backgroundJobId.ToString());
                db.BackgroundJobs.Remove(bgJob);
                db.SaveChanges();
                var job = db.Jobs.Include(j => j.BackgroundJobs).FirstOrDefault(j => j.Id == bgJob.JobId);;
                if (!job.Active && !job.BackgroundJobs.Any())
                    DeleteJob(new Guid(bgJob.JobId));
            }
        }

        public virtual void DeleteBackgroundJobs(Guid[] backgroundJobIds)
        {
        }

        public void DeleteJob(Guid jobId, bool deleteBackgroundJobs = false)
        {
            var db = GetDbContext();
            lock (db)
            {
                var job = db.Jobs.Include(j => j.BackgroundJobs).FirstOrDefault(j => j.Id == jobId.ToString());
                if (deleteBackgroundJobs)
                {
                    foreach (var bgJob in job.BackgroundJobs)
                        DeleteBackgroundJob(new Guid(bgJob.Id));
                }
                if (!db.BackgroundJobs.Any(j => j.JobId == jobId.ToString()))
                {
                    db.Jobs.Remove(job);
                    db.SaveChanges();
                }
            }
        }

        public virtual void DeleteExpired() { }

        public virtual void DeleteAll() { }        

        public Assembly GetStorageAssembly()
        {
            return Assembly.GetExecutingAssembly();
        }
    }
}