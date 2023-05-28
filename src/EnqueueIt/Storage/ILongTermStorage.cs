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
using System.Reflection;

namespace EnqueueIt
{
    public interface ILongTermStorage
    {
        int GetTotalFailedJobs();
        long GetBackgroundJobsCount(JobStatus status, string search = null, Guid? jobId = null);
        IEnumerable<BackgroundJob> GetBackgroundJobs(JobStatus status, string search = null,
            Guid? jobId = null, long start = 0, long end = 19);
        BackgroundJob GetLatestBackgroundJob(Guid jobId);
        Dictionary<string, Dictionary<JobStatus, int>> GetDailyStatus(DateTime time);
        BackgroundJob GetBackgroundJob(Guid backgroundJobId, bool includeDetails = true);
        Job GetJob(Guid jobId, bool loadLatest = false);
        void SaveBackgroundJobs(List<BackgroundJob> backgroundJobs);
        void DeleteBackgroundJob(Guid backgroundJobId);
        void DeleteBackgroundJobs(Guid[] backgroundJobIds);
        void DeleteJob(Guid jobId, bool deleteBackgroundJobs = false);
        void DeleteExpired();
        void DeleteAll();
        Assembly GetStorageAssembly();
    }
}
