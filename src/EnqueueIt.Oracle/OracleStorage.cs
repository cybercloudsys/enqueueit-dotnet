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
using Oracle.ManagedDataAccess.Client;

namespace EnqueueIt.Oracle
{
    public class OracleStorage : SqlStorage
    {
        public OracleStorage(string connectionString) : base(connectionString) { }
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
                var sql  = new StringBuilder();
                sql.Append("DELETE FROM \"background_jobs\" WHERE \"id\" IN (");
                for (int i = 0; i < backgroundJobIds.Length; i++)
                {
                    if (i > 0)
                        sql.Append(",");
                    sql.AppendFormat("{{{0}}}", i);
                }
                sql.Append(");");
                db.Database.ExecuteSqlRaw(sql.ToString(), Array.ConvertAll(backgroundJobIds, x => (object)x));
                db.Database.ExecuteSqlRaw("DELETE FROM \"jobs\" j WHERE NOT EXISTS(SELECT 1 FROM \"background_jobs\" WHERE \"job_id\" = j.\"id\")");
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
                jobs.Columns.Add("after_background_job_ids", typeof(string));
                jobs.Columns.Add("\"type\"", typeof(int));

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
                foreach (var bgJob in backgroundJobs)
                {
                    var item = EnqueueIt.Sql.Jobs.GetBackgroundJobItem(bgJob);
                    if (!bgJobIds.Contains(bgJob.Id))
                    {
                        if (!jobIds.Contains(bgJob.JobId))
                        {
                            jobIds.Add(bgJob.JobId);
                            jobs.Rows.Add(item.JobId, item.Job.Name, item.Job.Queue, item.Job.AppName,
                                item.Job.Argument, item.Job.CreatedAt, item.Job.IsRecurring, item.Job.StartAt,
                                item.Job.Active, item.Job.Recurring, item.Job.Tries,
                                item.Job.AfterBackgroundJobIds, int.Parse(item.Job.Type.ToString("D")));
                        }

                        bgJobIds.Add(bgJob.Id);
                        bgJobs.Rows.Add(item.Id, item.JobId, item.ProcessedBy, item.Server, item.CreatedAt, int.Parse(item.Status.ToString("D")),
                            item.JobError, item.StartedAt, item.CompletedAt, item.LastActivity, item.Logs);
                    }
                }
                var db = GetDbContext();
                lock (db)
                {
                    var conn = db.Database.GetDbConnection() as OracleConnection;
                    if (conn.State == ConnectionState.Closed || conn.State == ConnectionState.Broken)
                    conn.Open();

                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var cmd = new OracleCommand(@"CREATE GLOBAL TEMPORARY TABLE temp_jobs (id CHAR(36), name NVARCHAR2(2000),
                                queue NVARCHAR2(2000), app_name NVARCHAR2(2000), argument NVARCHAR2(2000), created_at TIMESTAMP(7),
                                is_recurring NUMBER(1), start_at TIMESTAMP(7), active NUMBER(1), recurring NVARCHAR2(2000), tries NUMBER(10),
                                after_background_job_ids NVARCHAR2(2000), ""type"" NUMBER(10))", conn))
                                cmd.ExecuteNonQuery();

                            using (var bulkCopy = new OracleBulkCopy(conn))
                            {
                                bulkCopy.DestinationTableName = "temp_jobs";
                                bulkCopy.WriteToServer(jobs);
                            }

                            using (var cmd = new OracleCommand(@"MERGE INTO ""jobs"" bj USING(SELECT id,name,queue,app_name,argument,
                                    created_at,is_recurring,start_at,active,recurring,tries,""type"",after_background_job_ids
                                FROM temp_jobs) src ON (bj.""id"" = src.id)
                                WHEN NOT MATCHED THEN
                                    INSERT (""id"",""name"",""queue"",""app_name"",""argument"",""created_at"",""is_recurring"",
                                        ""start_at"",""active"",""recurring"",""tries"",""type"",""after_background_job_ids"")
                                    VALUES (src.id,src.name,src.queue,src.app_name,src.argument,src.created_at,src.is_recurring,
                                        src.start_at,src.active,src.recurring,src.tries,src.""type"",src.after_background_job_ids)", conn))
                                cmd.ExecuteNonQuery();
                                                    
                            using (var cmd = new OracleCommand("DROP TABLE temp_jobs", conn))
                                cmd.ExecuteNonQuery();

                            using (var cmd = new OracleCommand(@"CREATE GLOBAL TEMPORARY TABLE temp_bg_jobs (id CHAR(36), job_id CHAR(36),
                                processed_by CHAR(36), server NVARCHAR2(2000), created_at TIMESTAMP(7),
                                status NUMBER(10), job_error NVARCHAR2(2000), started_at TIMESTAMP(7), completed_at TIMESTAMP(7),
                                last_activity TIMESTAMP(7), logs NVARCHAR2(2000))", conn))
                                cmd.ExecuteNonQuery();
                            
                            using (var bulkCopy = new OracleBulkCopy(conn))
                            {
                                bulkCopy.DestinationTableName = "temp_bg_jobs";
                                bulkCopy.WriteToServer(bgJobs);
                            }

                            using (var cmd = new OracleCommand(@"MERGE INTO ""background_jobs"" bj USING(SELECT id,job_id,processed_by,server,created_at,status,
                                    job_error,started_at, completed_at,last_activity,logs FROM temp_bg_jobs) src ON (bj.""id"" = src.id)
                                WHEN MATCHED THEN
                                    UPDATE SET bj.""job_id""=src.job_id,bj.""processed_by""=src.processed_by,bj.""server""=src.server,
                                        bj.""created_at""=src.created_at,bj.""status""=src.status,bj.""job_error""=src.job_error,
                                        bj.""started_at""=src.started_at,bj.""completed_at""=src.completed_at,
                                        bj.""last_activity""=src.last_activity,bj.""logs""=src.logs
                                WHEN NOT MATCHED THEN
                                    INSERT (""id"",""job_id"",""processed_by"",""server"",""created_at"",""status"",
                                        ""job_error"",""started_at"",""completed_at"",""last_activity"",""logs"")
                                    VALUES (src.id,src.job_id,src.processed_by,src.server,src.created_at,src.status,
                                        src.job_error,src.started_at,src.completed_at,src.last_activity,src.logs)", conn))
                                cmd.ExecuteNonQuery();
                            
                            using (var cmd = new OracleCommand("DROP TABLE temp_bg_jobs", conn))
                                cmd.ExecuteNonQuery();
                            
                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                        }
                    }
                }
            }
        }

        public override void DeleteExpired()
        {
            var db = GetDbContext();
            lock (db)
            {
                DateTime date = DateTime.UtcNow.AddDays(-GlobalConfiguration.Current.Configuration.StorageExpirationInDays);
                db.Database.ExecuteSqlRaw("DELETE FROM \"background_jobs\" WHERE \"completed_at\" < {0}", date);
                db.Database.ExecuteSqlRaw("DELETE FROM \"jobs\" WHERE NOT EXISTS (SELECT \"id\" FROM \"background_jobs\" WHERE \"job_id\" = \"jobs\".\"id\")");
            }
        }

        public override void DeleteAll()
        {
            var db = GetDbContext();
            lock (db)
            {
                db.Database.ExecuteSqlRaw("DELETE FROM \"background_jobs\"");
                db.Database.ExecuteSqlRaw("DELETE FROM \"jobs\"");
            }
        }
    }
}