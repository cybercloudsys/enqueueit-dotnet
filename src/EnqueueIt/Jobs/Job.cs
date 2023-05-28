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
using Recur;

namespace EnqueueIt
{
    /// <summary>
    /// The job information that is required for the background job to run as well as the future
    /// starting time and the frequency in case the job was scheduled
    /// </summary>
    public class Job
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Queue { get; set; }
        public string AppName { get; set; }
        public JobType Type { get; set; }
        public JobArgument JobArgument { get; set; }
        public string Argument { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRecurring { get; set; }
        public DateTime? StartAt { get; set; }
        public bool Active { get; set; }
        public RecurringPattern RecurringPattern { get; set; }
        public int Tries { get; set; }
        public string AfterBackgroundJobIds { get; set; }
        [JsonIgnore]
        public List<BackgroundJob> BackgroundJobs { get; set; }
    }
}