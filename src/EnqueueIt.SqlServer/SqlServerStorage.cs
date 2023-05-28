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
using System.Threading.Tasks;
using System.Linq;
using EnqueueIt.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EnqueueIt.SqlServer
{
    public class SqlServerStorage : SqlStorage
    {
        public SqlServerStorage(string connectionString) : base(connectionString) { }
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
                db.Database.ExecuteSqlRaw("DELETE FROM EnqueueIt.Background_Jobs WHERE Id IN ('" + string.Join("','", backgroundJobIds) + "')");
                db.Database.ExecuteSqlRaw("DELETE FROM EnqueueIt.Jobs WHERE NOT EXISTS(SELECT 1 FROM EnqueueIt.Background_Jobs WHERE Job_Id = EnqueueIt.Jobs.Id)");
            }
        }

        public override void SaveBackgroundJobs(List<BackgroundJob> backgroundJobs)
        {
            if (backgroundJobs.Count > 0)
            {
                var jobIds = new HashSet<Guid>();
                var bgJobIds = new HashSet<Guid>();
                var jobs = new DataTable();
                jobs.Columns.Add("id", typeof(string));
                jobs.Columns.Add("name", typeof(string));
                jobs.Columns.Add("queue", typeof(string));
                jobs.Columns.Add("app_name", typeof(string));
                jobs.Columns.Add("argument", typeof(string));
                jobs.Columns.Add("created_at", typeof(DateTime));
                jobs.Columns.Add("is_recurring", typeof(bool));
                jobs.Columns.Add("start_at", typeof(DateTime));
                jobs.Columns.Add("active", typeof(bool));
                jobs.Columns.Add("recurring", typeof(string));
                jobs.Columns.Add("tries", typeof(int));
                jobs.Columns.Add("type", typeof(int));
                jobs.Columns.Add("after_background_job_ids", typeof(string));

                var bgJobs = new DataTable();
                bgJobs.Columns.Add("id", typeof(string));
                bgJobs.Columns.Add("job_id", typeof(string));
                bgJobs.Columns.Add("processed_by", typeof(string));
                bgJobs.Columns.Add("server", typeof(string));
                bgJobs.Columns.Add("created_at", typeof(DateTime));
                bgJobs.Columns.Add("status", typeof(int));
                bgJobs.Columns.Add("job_error", typeof(string));
                bgJobs.Columns.Add("started_at", typeof(DateTime));
                bgJobs.Columns.Add("completed_at", typeof(DateTime));
                bgJobs.Columns.Add("last_activity", typeof(DateTime));
                bgJobs.Columns.Add("logs", typeof(string));
                var bgJobsParams = new List<object>();
                foreach (var bgJob in backgroundJobs)
                {
                    var item = EnqueueIt.Sql.Jobs.GetBackgroundJobItem(bgJob);
                    if (!bgJobIds.Contains(bgJob.Id))
                    {
                        if (!jobIds.Contains(bgJob.JobId))
                        {
                            jobIds.Add(bgJob.JobId);
                            jobs.Rows.Add(item.JobId, item.Job.Name, item.Job.Queue, item.Job.AppName,
                                item.Job.Argument, item.Job.CreatedAt, item.Job.IsRecurring, item.Job.StartAt, item.Job.Active, item.Job.Recurring,
                                item.Job.Tries, int.Parse(item.Job.Type.ToString("D")), item.Job.AfterBackgroundJobIds);
                        }

                        bgJobIds.Add(bgJob.Id);
                        bgJobs.Rows.Add(item.Id, item.JobId, item.ProcessedBy, item.Server, item.CreatedAt, int.Parse(item.Status.ToString("D")),
                            item.JobError, item.StartedAt, item.CompletedAt, item.LastActivity, item.Logs);
                    }
                }
                var db = GetDbContext();
                lock (db)
                {
                    while (!db.Database.CanConnect())
                        Task.Delay(1000).Wait();
                    var conn = db.Database.GetDbConnection() as SqlConnection;
                    if (conn.State == ConnectionState.Closed || conn.State == ConnectionState.Broken)
                        conn.Open();

                    var cmd = new SqlCommand(@"CREATE TABLE #TempJobs ([id] char(36) NOT NULL, [name] nvarchar(max) NULL,
                        [queue] nvarchar(max) NULL, [app_name] nvarchar(max) NULL, [argument] nvarchar(max) NULL, [created_at] datetime2 NOT NULL,
                        [is_recurring] bit NOT NULL, [start_at] datetime2 NULL, [active] bit NOT NULL, [recurring] nvarchar(max) NULL, [tries] int NOT NULL,
                        [type] int NOT NULL, [after_background_job_ids] nvarchar(max) NULL);
                        CREATE TABLE #TempBgJobs ([id] char(36) NOT NULL, [job_id] char(36) NOT NULL,
                        [processed_by] char(36) NULL, [server] nvarchar(max) NULL, [created_at] datetime2 NOT NULL,
                        [status] int NOT NULL, [job_error] nvarchar(max) NULL, [started_at] datetime2 NULL, [completed_at] datetime2 NULL,
                        [last_activity] datetime2 NULL, [logs] nvarchar(max) NULL);", conn);
                    cmd.ExecuteNonQuery();

                    using (var bulkCopy = new SqlBulkCopy(conn))
                    {
                        bulkCopy.DestinationTableName = "#TempJobs";
                        bulkCopy.WriteToServer(jobs.CreateDataReader());
                    }
                    using (var bulkCopy = new SqlBulkCopy(conn))
                    {
                        bulkCopy.DestinationTableName = "#TempBgJobs";
                        bulkCopy.WriteToServer(bgJobs.CreateDataReader());
                    }

                    cmd = new SqlCommand(@"MERGE INTO EnqueueIt.jobs jobs USING (SELECT id,name,queue,app_name,argument,created_at,
                        is_recurring,start_at,active,recurring,tries,type,after_background_job_ids FROM #TempJobs) AS dt ON dt.id = jobs.id
                        WHEN NOT MATCHED BY TARGET THEN INSERT (id,name,queue,app_name,argument,created_at,is_recurring,start_at,
                            active,recurring,tries,type,after_background_job_ids)
                        VALUES (id,name,queue,app_name,argument,created_at,is_recurring,start_at,active,recurring,tries,type,after_background_job_ids);
                        DROP TABLE #TempJobs;
                        MERGE INTO EnqueueIt.background_jobs bgjobs USING (SELECT id,job_id,processed_by,server,
                        created_at, status,job_error,started_at, completed_at,last_activity,logs FROM #TempBgJobs) AS dt ON dt.id = bgjobs.id
                        WHEN MATCHED THEN UPDATE SET job_id=dt.job_id,processed_by=dt.processed_by,server=dt.server,status=dt.status,
                            job_error=dt.job_error,started_at=dt.started_at,completed_at=dt.completed_at,last_activity=dt.last_activity,logs=dt.logs
                        WHEN NOT MATCHED BY TARGET THEN INSERT (id,job_id,processed_by,server,created_at,status,job_error,started_at,completed_at,last_activity,logs)
                        VALUES (id,job_id,processed_by,server,created_at,status,job_error,started_at,completed_at,last_activity,logs);
                        DROP TABLE #TempBgJobs;", conn);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public override void DeleteExpired()
        {
            var db = GetDbContext();
            lock (db)
            {
                var date = DateTime.UtcNow.AddDays(-GlobalConfiguration.Current.Configuration.StorageExpirationInDays);
                db.Database.ExecuteSqlRaw("DELETE FROM EnqueueIt.Background_Jobs WHERE completed_at < {0}", date);
                db.Database.ExecuteSqlRaw("DELETE FROM EnqueueIt.Jobs WHERE NOT EXISTS(SELECT id FROM EnqueueIt.Background_Jobs WHERE job_id = EnqueueIt.Jobs.id)");
            }
        }

        public override void DeleteAll()
        {
            var db = GetDbContext();
            lock (db)
            {
                db.Database.ExecuteSqlRaw("DELETE FROM EnqueueIt.Background_Jobs");
                db.Database.ExecuteSqlRaw("DELETE FROM EnqueueIt.Jobs");
            }
        }
    }
}