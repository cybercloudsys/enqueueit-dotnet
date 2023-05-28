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
using System.Linq;
using Recur;

namespace EnqueueIt.Dashboard
{
    public class JobListItem
    {
        public JobListItem() { }
        public JobListItem(BackgroundJob backgroundJob)
        {
            Id = backgroundJob.Id;
            JobId = backgroundJob.JobId;
            ProcessedBy = backgroundJob.ProcessedBy;
            Server = backgroundJob.Server;
            Queue = backgroundJob.Job.Queue;
            Status = backgroundJob.Status;
            Error = backgroundJob.Error;
            CreatedAt = backgroundJob.CreatedAt;
            StartedAt = backgroundJob.StartedAt;
            CompletedAt = backgroundJob.CompletedAt;
            LastActivity = backgroundJob.LastActivity;
            Argument = backgroundJob.Job.Argument;
            if (StartedAt.HasValue)
            {
                var time = ((backgroundJob.CompletedAt ?? DateTime.UtcNow) - backgroundJob.StartedAt).Value;
                if (time.TotalSeconds < 1)
                    Duration = "< second";
                else
                    Duration = time.ToString().Split('.')[0];
            }
            Name = backgroundJob.Job.Name;
            if (backgroundJob.Job.JobArgument?.ClassType != null)
            {
                Assembly = backgroundJob.Job.JobArgument.Assembly;
                ClassType = backgroundJob.Job.JobArgument.ClassType.Split(',')[0].Split('.').Last();
                MethodName = backgroundJob.Job.JobArgument.MethodName;
            }
            else if (string.IsNullOrWhiteSpace(backgroundJob.Job.Name))
                Name = backgroundJob.Job.AppName;
            IsRecurring = backgroundJob.Job.IsRecurring;
            StartAt = backgroundJob.Job.StartAt;
            RecurringPattern = backgroundJob.Job.RecurringPattern;
        }

        public JobListItem(Job job)
        {
            Id = job.Id;
            Name = job.Name;
            Queue = job.Queue;
            Status = JobStatus.Scheduled;
            CreatedAt = job.CreatedAt;
            Argument = job.Argument;
            if (job.JobArgument?.ClassType != null)
            {
                Assembly = job.JobArgument.Assembly;
                ClassType = job.JobArgument.ClassType.Split(',')[0].Split('.').Last();
                MethodName = job.JobArgument.MethodName;
            }
            else if (string.IsNullOrWhiteSpace(job.Name))
                Name = job.AppName;
            IsRecurring = job.IsRecurring;
            StartAt = job.StartAt ?? (job.IsRecurring ? job.RecurringPattern.NextTime() : (DateTime?)null);
            RecurringPattern = job.RecurringPattern;
            if (!string.IsNullOrWhiteSpace(job.AfterBackgroundJobIds))
                AfterBackgroundJobIds = job.AfterBackgroundJobIds.Split(',');
            if (job.BackgroundJobs != null && job.BackgroundJobs.Count > 0)
            {
                JobId = job.BackgroundJobs[0].Id;
                ProcessedBy = job.BackgroundJobs[0].ProcessedBy;
                Server = job.BackgroundJobs[0].Server;
                Status = job.BackgroundJobs[0].Status;
                Error = job.BackgroundJobs[0].Error;
                StartedAt = job.BackgroundJobs[0].StartedAt;
                CompletedAt = job.BackgroundJobs[0].CompletedAt;
            }
        }

        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        public string Name { get; set; }
        public Guid? ProcessedBy { get; set; }
        public string Server { get; set; }
        public string Queue { get; set; }
        public DateTime CreatedAt { get; set; }
        public JobStatus Status { get; set; }
        public JobError Error { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? LastActivity { get; set; }
        public string Duration { get; set; }
        public string Assembly { get; set; }
        public string Environment { get; set; }
        public string ClassType { get; set; }
        public string MethodName { get; set; }
        public string Argument { get; set; }
        public bool IsRecurring { get; set; }
        public DateTime? StartAt { get; set; }
        public RecurringPattern RecurringPattern { get; set; }
        public string[] AfterBackgroundJobIds { get; set; }
        public int SubJobs { get; set; }
    }
}