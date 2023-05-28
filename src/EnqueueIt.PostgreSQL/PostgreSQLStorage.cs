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
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using EnqueueIt.Sql;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Npgsql;
using NpgsqlTypes;

namespace EnqueueIt.PostgreSQL
{
    public class PostgreSQLStorage : SqlStorage
    {
        public PostgreSQLStorage(string connectionString) : base(connectionString) { }
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
                db.Database.ExecuteSqlRaw("DELETE FROM \"EnqueueIt\".background_jobs WHERE id IN ('" + string.Join("','", backgroundJobIds) + "')");
                db.Database.ExecuteSqlRaw("DELETE FROM \"EnqueueIt\".jobs j WHERE NOT EXISTS(SELECT 1 FROM \"EnqueueIt\".background_jobs WHERE job_id = j.id)");
            }
        }

        public override void SaveBackgroundJobs(List<BackgroundJob> backgroundJobs)
        {
            if (backgroundJobs.Count > 0)
            {
                var jobIds = new HashSet<Guid>();
                var bgJobIds = new HashSet<Guid>();
                var db = GetDbContext();
                lock (db)
                {
                    var conn = db.Database.GetDbConnection() as NpgsqlConnection;
                    if (conn.State == ConnectionState.Closed || conn.State == ConnectionState.Broken)
                        conn.Open();

                    var cmd = new NpgsqlCommand(@"CREATE TEMP TABLE temp_jobs
                        (id char(36) NOT NULL, name text, queue text, app_name text, argument text,
                        created_at timestamp without time zone NOT NULL, is_recurring boolean NOT NULL,
                        start_at timestamp without time zone, active boolean NOT NULL, recurring text,
                        tries int, type smallint, after_background_job_ids text);", conn);
                    cmd.ExecuteNonQuery();

                    cmd = new NpgsqlCommand(@"CREATE TEMP TABLE temp_bg_jobs
                        (id char(36) NOT NULL, job_id char(36), processed_by char(36), server text,
                        created_at timestamp without time zone NOT NULL, status smallint NOT NULL,
                        job_error text, started_at timestamp without time zone,
                        completed_at timestamp without time zone,
                        last_activity timestamp without time zone, logs text);", conn);
                    cmd.ExecuteNonQuery();

                    using (var writer = conn.BeginBinaryImport(@"COPY temp_jobs (id,name,queue,app_name,argument,created_at,
                        is_recurring,start_at,active,recurring,tries,type,after_background_job_ids) FROM STDIN BINARY"))
                    {
                        foreach (var bgJob in backgroundJobs)
                        {
                            if (!bgJobIds.Contains(bgJob.Id) && !jobIds.Contains(bgJob.JobId))
                            {
                                var item = EnqueueIt.Sql.Jobs.GetJobItem(bgJob.Job);
                                jobIds.Add(bgJob.JobId);
                                writer.StartRow();
                                writer.Write(item.Id, NpgsqlDbType.Char);
                                writer.Write(item.Name, NpgsqlDbType.Text);
                                writer.Write(item.Queue, NpgsqlDbType.Text);
                                writer.Write(item.AppName, NpgsqlDbType.Text);
                                writer.Write(item.Argument, NpgsqlDbType.Text);
                                writer.Write(item.CreatedAt, NpgsqlDbType.Timestamp);
                                writer.Write(item.IsRecurring, NpgsqlDbType.Boolean);
                                writer.Write(item.StartAt, NpgsqlDbType.Timestamp);
                                writer.Write(item.Active, NpgsqlDbType.Boolean);
                                writer.Write(item.Recurring, NpgsqlDbType.Text);
                                writer.Write(item.Tries, NpgsqlDbType.Integer);
                                writer.Write(int.Parse(item.Type.ToString("D")), NpgsqlDbType.Smallint);
                                writer.Write(item.AfterBackgroundJobIds, NpgsqlDbType.Text);
                            }
                        }
                        writer.Complete();
                    }

                    using (var writer = conn.BeginBinaryImport(@"COPY temp_bg_jobs (id,job_id,processed_by,server,created_at,
                        status,job_error,started_at, completed_at,last_activity,logs) FROM STDIN BINARY"))
                    {
                        foreach (var bgJob in backgroundJobs)
                        {
                            var item = EnqueueIt.Sql.Jobs.GetBackgroundJobItem(bgJob, false);
                            if (!bgJobIds.Contains(bgJob.Id))
                            {
                                bgJobIds.Add(bgJob.Id);
                                writer.StartRow();
                                writer.Write(item.Id, NpgsqlDbType.Char);
                                writer.Write(item.JobId, NpgsqlDbType.Char);
                                writer.Write(item.ProcessedBy, NpgsqlDbType.Char);
                                writer.Write(item.Server, NpgsqlDbType.Text);
                                writer.Write(item.CreatedAt, NpgsqlDbType.Timestamp);
                                writer.Write(int.Parse(item.Status.ToString("D")), NpgsqlDbType.Smallint);
                                writer.Write(item.JobError, NpgsqlDbType.Text);
                                writer.Write(item.StartedAt.Value, NpgsqlDbType.Timestamp);
                                writer.Write(item.CompletedAt.Value, NpgsqlDbType.Timestamp);
                                writer.Write(item.LastActivity.HasValue ? item.LastActivity : (object)DBNull.Value, NpgsqlDbType.Timestamp);
                                writer.Write(item.Logs, NpgsqlDbType.Text);
                            }
                        }
                        writer.Complete();
                    }

                    cmd = new NpgsqlCommand(@"INSERT INTO ""EnqueueIt"".jobs (id,name,queue,app_name,argument,created_at,is_recurring,start_at,active,recurring,tries,type,after_background_job_ids)
                        SELECT id,name,queue,app_name,argument,created_at, is_recurring,start_at,active,recurring,tries,type,after_background_job_ids FROM temp_jobs
                        ON CONFLICT DO NOTHING;
                        INSERT INTO ""EnqueueIt"".background_jobs (id,job_id,processed_by,server,created_at,status,job_error,started_at,completed_at,last_activity,logs)
                        SELECT id,job_id,processed_by,server,created_at,status,job_error,started_at,completed_at,last_activity,logs FROM temp_bg_jobs
                        ON CONFLICT (id) DO UPDATE SET job_id=excluded.job_id,processed_by=excluded.processed_by,server=excluded.server,status=excluded.status,
                            job_error=excluded.job_error,started_at=excluded.started_at,completed_at=excluded.completed_at,last_activity=excluded.last_activity,logs=excluded.logs;
                        DROP TABLE temp_jobs; DROP TABLE temp_bg_jobs;", conn);
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
                db.Database.ExecuteSqlRaw("DELETE FROM \"EnqueueIt\".background_jobs WHERE completed_at < {0}", date);
                db.Database.ExecuteSqlRaw("DELETE FROM \"EnqueueIt\".jobs jobs WHERE NOT EXISTS(SELECT id FROM \"EnqueueIt\".background_jobs WHERE job_id = jobs.id)");
            }
        }

        public override void DeleteAll()
        {
            var db = GetDbContext();
            lock (db)
            {
                db.Database.ExecuteSqlRaw("DELETE FROM \"EnqueueIt\".background_jobs");
                db.Database.ExecuteSqlRaw("DELETE FROM \"EnqueueIt\".jobs jobs");
            }
        }
    }
}