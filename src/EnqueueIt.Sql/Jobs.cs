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

using System.Text.Json;
using System.Collections.Generic;
using System;
using Recur;

namespace EnqueueIt.Sql
{
    public static class Jobs
    {
        public static Job GetJob(JobItem jobItem)
        {
            var job = new Job {
                Id = new Guid(jobItem.Id),
                Name = jobItem.Name,
                Queue = jobItem.Queue,
                AppName = jobItem.AppName,
                CreatedAt = jobItem.CreatedAt,
                IsRecurring = jobItem.IsRecurring,
                StartAt = jobItem.StartAt,
                Active = jobItem.Active,
                Tries = jobItem.Tries,
                Type = jobItem.Type,
                AfterBackgroundJobIds = jobItem.AfterBackgroundJobIds
            };
            if (!string.IsNullOrWhiteSpace(jobItem.Argument))
            {
                try
                {
                    job.JobArgument = (JobArgument)JsonSerializer.Deserialize(jobItem.Argument, typeof(JobArgument));
                }
                catch { }
                if (job.JobArgument?.ClassType == null)
                    job.Argument = jobItem.Argument;
            }
            if (!string.IsNullOrWhiteSpace(jobItem.Recurring))
                job.RecurringPattern = (RecurringPattern)JsonSerializer.Deserialize(jobItem.Recurring, typeof(RecurringPattern));
            return job;
        }

        public static BackgroundJob GetBackgroundJob(BackgroundJobItem bgJobItem)
        {
            var bgJob = new BackgroundJob {
                Id = new Guid(bgJobItem.Id),
                JobId = new Guid(bgJobItem.JobId),
                ProcessedBy = string.IsNullOrWhiteSpace(bgJobItem.ProcessedBy) ? new Guid?() : new Guid(bgJobItem.ProcessedBy),
                Server = bgJobItem.Server,
                CreatedAt = bgJobItem.CreatedAt,
                Status = bgJobItem.Status,
                StartedAt = bgJobItem.StartedAt,
                CompletedAt = bgJobItem.CompletedAt,
                LastActivity = bgJobItem.LastActivity
            };
            if (!string.IsNullOrWhiteSpace(bgJobItem.JobError))
                bgJob.Error = (JobError)JsonSerializer.Deserialize(bgJobItem.JobError, typeof(JobError));
            if (!string.IsNullOrEmpty(bgJobItem.Logs))
                bgJob.JobLogs = (List<JobLog>)JsonSerializer.Deserialize(bgJobItem.Logs, typeof(List<JobLog>));
            else
                bgJob.JobLogs = new List<JobLog>();
            if (bgJobItem.Job != null)
                bgJob.Job = GetJob(bgJobItem.Job);
            return bgJob;
        }

        public static JobItem GetJobItem(Job job)
        {
            var jobItem = new JobItem {
                Id = job.Id.ToString(),
                Name = job.Name,
                Queue = job.Queue,
                AppName = job.AppName,
                CreatedAt = job.CreatedAt,
                IsRecurring = job.IsRecurring,
                StartAt = job.StartAt,
                Active = job.Active,
                Tries = job.Tries,
                Type = job.Type,
                Argument = job.Argument,
                AfterBackgroundJobIds = job.AfterBackgroundJobIds
            };
            if (job.JobArgument != null)
                jobItem.Argument = Serializer.Serialize(job.JobArgument);
            if (job.RecurringPattern != null)
                jobItem.Recurring = Serializer.Serialize(job.RecurringPattern);
            return jobItem;
        }

        public static BackgroundJobItem GetBackgroundJobItem(BackgroundJob bgJob, bool includeDetails = true)
        {
            var bgJobItem = new BackgroundJobItem {
                Id = bgJob.Id.ToString(),
                JobId = bgJob.JobId.ToString(),
                ProcessedBy = bgJob.ProcessedBy?.ToString(),
                Server = bgJob.Server,
                CreatedAt = bgJob.CreatedAt,
                Status = bgJob.Status,
                StartedAt = bgJob.StartedAt,
                CompletedAt = bgJob.CompletedAt,
                LastActivity = bgJob.LastActivity,
            };
            if (bgJob.Error != null)
                bgJobItem.JobError = Serializer.Serialize(bgJob.Error);
            if (bgJob.JobLogs != null && bgJob.JobLogs.Count > 0)
                bgJobItem.Logs = Serializer.Serialize(bgJob.JobLogs);
            if (bgJob.Job != null)
                bgJobItem.Job = GetJobItem(bgJob.Job);
            return bgJobItem;
        }
    }
}