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
using System.Text.Json.Serialization;

namespace EnqueueIt
{
    /// <summary>
    /// The information of a job instance was enqueued and it has the status and the result of the job execution.
    /// </summary>
    public class BackgroundJob
    {
        public Guid Id { get; set; }
        public Guid JobId { get; set; }
        [JsonIgnore]
        public Job Job { get; set; }
        public Guid? ProcessedBy { get; set; }
        public string Server { get; set; }
        public DateTime CreatedAt { get; set; }
        public JobStatus Status { get; set; }
        public JobError Error { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? LastActivity { get; set; }
        [JsonIgnore]
        public List<JobLog> JobLogs { get; set; }

        internal void Completed()
        {
            if (Error != null)
                Status = JobStatus.Failed;
            else
                Status = JobStatus.Processed;
            CompletedAt = DateTime.UtcNow;
        }
    }
}