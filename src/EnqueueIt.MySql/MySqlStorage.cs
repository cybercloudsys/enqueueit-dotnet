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
using System.Data;
using System.Text;
using EnqueueIt.Sql;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;

namespace EnqueueIt.MySql
{
    public class MySqlStorage : SqlStorage
    {
        public MySqlStorage(string connectionString) : base(connectionString) { }
        protected override StorageDbContext GetDbContext()
        {
            if (dbContext == null)
            {
                dbContext = new EnqueueItDbContext();
                dbContext.Database.Migrate();
            }
            return dbContext;
        }
        
        public override void DeleteBackgroundJobs(Guid[] backgroundJobIds)
        {
            var db = GetDbContext();
            lock (db)
            {
                db.Database.ExecuteSqlRaw("DELETE FROM background_jobs WHERE id IN ('" + string.Join("','", backgroundJobIds) + "')");
                db.Database.ExecuteSqlRaw("DELETE FROM jobs j WHERE NOT EXISTS(SELECT 1 FROM background_jobs WHERE job_id = j.id)");
            }
        }

        public override void SaveBackgroundJobs(List<BackgroundJob> backgroundJobs)
        {
            if (backgroundJobs.Count > 0)
            {
                var jobIds = new HashSet<Guid>();
                var bgJobIds = new HashSet<Guid>();
                var jobs = new StringBuilder();
                var bgJobs = new StringBuilder();
                jobs.Append("INSERT INTO TempJobs (id,name,queue,app_name,argument,created_at,is_recurring,start_at,active,recurring,tries,type,after_background_job_ids) VALUES ");
                bgJobs.Append("INSERT INTO TempBgJobs (id,job_id,processed_by,server,created_at,status,job_error,started_at,completed_at,last_activity,logs) VALUES ");
                var jobsParams = new List<MySqlParameter>();
                var bgJobsParams = new List<MySqlParameter>();
                int j = 0, b = 0;
                foreach (var bgJob in backgroundJobs)
                {
                    BackgroundJobItem item = Sql.Jobs.GetBackgroundJobItem(bgJob);
                    if (!bgJobIds.Contains(bgJob.Id))
                    {
                        if (!jobIds.Contains(bgJob.JobId))
                        {
                            jobIds.Add(bgJob.JobId);
                            if (j > 0)
                                jobs.Append(",");
                            var jIx = j*13;
                            jobsParams.Add(new MySqlParameter("@p" + (jIx), item.JobId));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+1), item.Job.Name));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+2), item.Job.Queue));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+3), item.Job.AppName));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+4), item.Job.Argument));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+5), item.Job.CreatedAt));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+6), item.Job.IsRecurring));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+7), item.Job.StartAt));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+8), item.Job.Active));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+9), item.Job.Recurring));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+10), item.Job.Tries));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+11), item.Job.Type));
                            jobsParams.Add(new MySqlParameter("@p" + (jIx+12), item.Job.AfterBackgroundJobIds));
                            jobs.Append("(");
                            for (int i = 0; i < 12; i++)
                            {
                                jobs.Append(jobsParams[jIx + i].ParameterName);
                                jobs.Append(",");
                            }
                            jobs.Append(jobsParams[jIx+12].ParameterName);
                            jobs.Append(")");
                            j++;
                        }
                        bgJobIds.Add(bgJob.Id);
                        if (b > 0)
                            bgJobs.Append(",");
                        var bRowIx = b * 11;
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx), item.Id));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+1), item.JobId));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+2), item.ProcessedBy));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+3), item.Server));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+4), item.CreatedAt));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+5), item.Status));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+6), item.JobError));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+7), item.StartedAt));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+8), item.CompletedAt));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+9), item.LastActivity));
                        bgJobsParams.Add(new MySqlParameter("@p" + (bRowIx+10), item.Logs));
                        bgJobs.Append("(");
                        for (int i = 0; i < 10; i++)
                        {
                            bgJobs.Append(bgJobsParams[bRowIx + i].ParameterName);
                            bgJobs.Append(",");
                        }
                        bgJobs.Append(bgJobsParams[bRowIx+10].ParameterName);
                        bgJobs.Append(")");
                        b++;
                    }
                }
                jobs.Append(";");
                bgJobs.Append(";");
                var db = GetDbContext();
                lock (db)
                {
                    var conn = db.Database.GetDbConnection() as MySqlConnection;
                    if (conn.State == ConnectionState.Closed || conn.State == ConnectionState.Broken)
                        conn.Open();

                    var cmd = new MySqlCommand(@"CREATE TEMPORARY TABLE TempJobs (id char(36) NOT NULL, name text NULL,
                        queue text NULL, app_name text NULL, argument text NULL, created_at datetime(6) NOT NULL,
                        is_recurring tinyint(1) NOT NULL, start_at datetime(6) NULL, active tinyint(1) NOT NULL, recurring text NULL, tries int NOT NULL,
                        type int NOT NULL, after_background_job_ids text NULL);
                        CREATE TEMPORARY TABLE TempBgJobs (id char(36) NOT NULL, job_id char(36) NOT NULL,
                        processed_by char(36) NULL, server text NULL, created_at datetime(6) NOT NULL,
                        status int NOT NULL, job_error text NULL, started_at datetime(6) NULL, completed_at datetime(6) NULL,
                        last_activity datetime(6) NULL, logs text NULL);", conn);
                    cmd.ExecuteNonQuery();

                    cmd = new MySqlCommand(jobs.ToString(), conn);
                    cmd.Parameters.AddRange(jobsParams.ToArray());
                    cmd.ExecuteNonQuery();

                    cmd = new MySqlCommand(bgJobs.ToString(), conn);
                    cmd.Parameters.AddRange(bgJobsParams.ToArray());
                    cmd.ExecuteNonQuery();

                    cmd = new MySqlCommand(@"INSERT IGNORE INTO jobs (id,name,queue,app_name,argument,created_at,is_recurring,start_at,active,recurring,tries,type,after_background_job_ids)
                        SELECT id,name,queue,app_name,argument,created_at,is_recurring,start_at,active,recurring,tries,type,after_background_job_ids FROM TempJobs;
                        DROP TABLE TempJobs;
                        INSERT INTO background_jobs (id,job_id,processed_by,server,created_at,status,job_error,started_at,completed_at,last_activity,logs)
                        SELECT id,job_id,processed_by,server,created_at, status,job_error,started_at, completed_at,last_activity,logs FROM TempBgJobs tb
                        ON DUPLICATE KEY UPDATE job_id=tb.job_id,processed_by=tb.processed_by,server=tb.server,status=tb.status,job_error=tb.job_error,
                            started_at=tb.started_at,completed_at=tb.completed_at,last_activity=tb.last_activity,logs=tb.logs;
                        DROP TABLE TempBgJobs;", conn);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public override void DeleteExpired()
        {
            var db = GetDbContext();
            lock (db)
            {
                DateTime date = DateTime.UtcNow.AddDays(-GlobalConfiguration.Current.Configuration.StorageExpirationInDays);
                db.Database.ExecuteSqlRaw("DELETE FROM background_jobs WHERE completed_at < {0}", date);
                db.Database.ExecuteSqlRaw("DELETE FROM jobs WHERE NOT EXISTS (SELECT id FROM background_jobs WHERE job_id = jobs.id)");
            }
        }

        public override void DeleteAll()
        {
            var db = GetDbContext();
            lock (db)
            {
                db.Database.ExecuteSqlRaw("DELETE FROM background_jobs");
                db.Database.ExecuteSqlRaw("DELETE FROM jobs");
            }
        }
    }
}