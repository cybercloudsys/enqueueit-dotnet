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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnqueueIt.Dashboard.Controllers
{
    [Authorize(Policy="EnqueueIt")]
    public class EnqueueItController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult GetJobs(int days = 0)
        {
            if (GlobalConfiguration.Current.LongTermStorage != null)
                return Ok(new { Time = DateTime.UtcNow, Data = GlobalConfiguration.Current.LongTermStorage
                    .GetDailyStatus(DateTime.UtcNow.AddDays(-days)) });
            else
                return Ok(new { Time = DateTime.UtcNow, Data = GlobalConfiguration.Current.Storage
                    .GetDailyStatus(DateTime.UtcNow.AddDays(-days)) });
        }

        public IActionResult DeleteAll()
        {
            GlobalConfiguration.Current.Storage.DeleteAll();
            if (GlobalConfiguration.Current.LongTermStorage != null)
                GlobalConfiguration.Current.LongTermStorage.DeleteAll();
            return RedirectToAction("Index");
        }

        public IActionResult Queues(string queue, int page = 1, int pageSize = 20)
        {
            var jobList = new JobList { Page = page, PageSize = pageSize,
                Jobs = new List<JobListItem>(), TotalJobs = new Dictionary<string, long>() };
            long start = (page - 1) * pageSize;
            long end = pageSize + start - 1;
            foreach (string que in GlobalConfiguration.Current.Storage.GetQueues())
                jobList.TotalJobs.Add(que, GlobalConfiguration.Current.Storage.GetQueueJobsCount(que));
            if (jobList.TotalJobs.Keys.Count > 0)
            {
                if (queue == null)
                    queue = jobList.TotalJobs.Keys.First();
                jobList.Status = queue;
                long jobsCount = jobList.TotalJobs[queue];
                jobList.TotalPages = (int)Math.Ceiling((double)jobsCount / jobList.PageSize);
                if (jobsCount > start)
                {
                    foreach(var job in GlobalConfiguration.Current.Storage
                        .GetQueueJobs(queue, start, jobsCount > end ? end : jobsCount))
                        jobList.Jobs.Add(new JobListItem(job));
                }
            }
            return View(jobList);
        }

        public IActionResult BgJobs(JobStatus status = JobStatus.Processing,
            string search = null, Guid? jobId = null, int page = 1, int pageSize = 20)
        {
            var jobList = new JobList { Page = page, PageSize = pageSize, ParentId = jobId, Search = search,
                Jobs = new List<JobListItem>(), TotalJobs = new Dictionary<string, long>() };
            long start = (page - 1) * pageSize;
            long end = pageSize + start - 1;
            var ltStorage = GlobalConfiguration.Current.LongTermStorage;
            var storage = GlobalConfiguration.Current.Storage;
            foreach (var st in ((JobStatus[])Enum.GetValues(typeof(JobStatus))).Where(st => st != JobStatus.Scheduled))
            {
                if (ltStorage != null && (st == JobStatus.Processed || st == JobStatus.Failed))
                    jobList.TotalJobs.Add(st.ToString(), ltStorage.GetBackgroundJobsCount(st, search, jobId));
                else
                    jobList.TotalJobs.Add(st.ToString(), storage.GetBackgroundJobsCount(st, jobId));
            }
            jobList.Status = status.ToString();
            long jobsCount = jobList.TotalJobs[jobList.Status];
            jobList.TotalPages = (int)Math.Ceiling((double)jobsCount / jobList.PageSize);
            if (ltStorage != null && (status == JobStatus.Processed || status == JobStatus.Failed))
            {
                if (jobsCount > start)
                {
                    foreach(var job in ltStorage.GetBackgroundJobs(status, search,
                        jobId, start, jobsCount > end ? end : (int)jobsCount))
                        jobList.Jobs.Add(new JobListItem(job));
                }
                if (jobId.HasValue)
                    jobList.Parent = new JobListItem(ltStorage.GetJob(jobId.Value));
            }
            else
            {
                if (jobsCount > start)
                {
                    foreach (var job in storage.GetBackgroundJobs(status, jobId,
                        start, jobsCount > end ? end : (int)jobsCount))
                        jobList.Jobs.Add(new JobListItem(job));
                }
                if (jobId.HasValue)
                    jobList.Parent = new JobListItem(storage.GetJob(jobId.Value));
            }
            return View(jobList);
        }

        public IActionResult Jobs(string status = "Scheduled", int page = 1, int pageSize = 20)
        {
            var jobList = new JobList { Page = page, PageSize = pageSize,
                Jobs = new List<JobListItem>(), TotalJobs = new Dictionary<string, long>() };
            long start = (page - 1) * pageSize;
            long end = pageSize + page - 1;
            jobList.TotalJobs.Add("Scheduled", GlobalConfiguration.Current.Storage.GetScheduledJobsCount("Scheduled"));
            jobList.TotalJobs.Add("Recurring", GlobalConfiguration.Current.Storage.GetScheduledJobsCount("Recurring"));
            jobList.TotalJobs.Add("Waiting", GlobalConfiguration.Current.Storage.GetScheduledJobsCount("Waiting"));
            if (jobList.TotalJobs.Keys.Count > 0)
            {
                jobList.Status = status;
                long jobsCount = jobList.TotalJobs[status];
                jobList.TotalPages = (int)Math.Ceiling((double)jobsCount / jobList.PageSize);
                if (jobsCount > start)
                {
                    foreach (var job in GlobalConfiguration.Current.Storage
                        .GetScheduledJobs(status, start, jobsCount > end ? end : jobsCount))
                    {
                        if (job.BackgroundJobs.Count == 0)
                        {
                            BackgroundJob bgJob;
                            if (GlobalConfiguration.Current.LongTermStorage != null)
                                bgJob = GlobalConfiguration.Current.LongTermStorage.GetLatestBackgroundJob(job.Id);
                            else
                                bgJob = GlobalConfiguration.Current.Storage.GetLatestBackgroundJob(job.Id);
                            if (bgJob != null)
                                job.BackgroundJobs.Add(bgJob);
                        }
                        jobList.Jobs.Add(new JobListItem(job));
                    }
                }
            }
            return View(jobList);
        }

        public IActionResult BgJob(Guid id)
        {
            var bgJob = GlobalConfiguration.Current.Storage.GetBackgroundJob(id);
            if (bgJob != null)
                bgJob.JobLogs = GlobalConfiguration.Current.Storage.GetJobLogs(id);
            else if (GlobalConfiguration.Current.LongTermStorage != null)
                bgJob = GlobalConfiguration.Current.LongTermStorage.GetBackgroundJob(id);
            return View(bgJob);
        }

        public IActionResult Job(Guid id)
        {
            var job = GlobalConfiguration.Current.Storage.GetJob(id, true);
            if (job == null)
                return NotFound();
            if (job.IsRecurring || job.StartAt.HasValue || !string.IsNullOrWhiteSpace(job.AfterBackgroundJobIds))
            {
                if (job.BackgroundJobs.Count == 0)
                {
                    BackgroundJob bgJob;
                    if (GlobalConfiguration.Current.LongTermStorage != null)
                        bgJob = GlobalConfiguration.Current.LongTermStorage.GetLatestBackgroundJob(job.Id);
                    else
                        bgJob = GlobalConfiguration.Current.Storage.GetLatestBackgroundJob(job.Id);
                    if (bgJob != null)
                        job.BackgroundJobs.Add(bgJob);
                }
                return View(job);
            }
            else
                return RedirectToAction(nameof(BgJob), new { id = job.BackgroundJobs.FirstOrDefault()?.Id });
        }

        public IActionResult Enqueue(Guid id)
        {
            var jobId = EnqueueIt.BackgroundJobs.EnqueueById(id);
            if (jobId != null)
                return Ok(jobId);
            else
                return NotFound();
        }

        public IActionResult Servers()
        {
            return View(GlobalConfiguration.Current.Storage.GetServers());
        }

        public IActionResult StopServer(Guid id)
        {
            if (GlobalConfiguration.Current.Configuration.EnableStopServers)
                EnqueueIt.Servers.Stop(id);
            return RedirectToAction(nameof(Servers));
        }

        public IActionResult StopJob(Guid id)
        {
            EnqueueIt.BackgroundJobs.Stop(id);
            return RedirectToAction(nameof(BgJob), new { id = id });
        }

        public IActionResult DeleteBackgroundJob(Guid id, string status)
        {
            JobStatus jStatus = Enum.Parse<JobStatus>(status);
            if (jStatus == JobStatus.Processed || jStatus == JobStatus.Failed)
                GlobalConfiguration.Current.LongTermStorage.DeleteBackgroundJob(id);
            else
                GlobalConfiguration.Current.Storage.DeleteBackgroundJob(id);
            return RedirectToAction(nameof(BgJobs), new { status = status });
        }

        public IActionResult DeleteJob(Guid id)
        {
            GlobalConfiguration.Current.Storage.DeleteJob(id);
            return RedirectToAction(nameof(BgJobs));
        }

        public IActionResult RetryJob(Guid id)
        {
            return RedirectToAction(nameof(BgJob), new { id = BackgroundJobs.ReEnqueue(id) });
        }

        [HttpPost]
        public IActionResult StopJobs(Guid[] ids)
        {
            foreach(Guid id in ids)
                BackgroundJobs.Stop(id);
            return Json(new { });
        }

        [HttpPost]
        public IActionResult DeleteJobs(Guid[] ids, string status)
        {
            JobStatus jStatus = JobStatus.Enqueued;
            if (!string.IsNullOrWhiteSpace(status))
                jStatus = (JobStatus)Enum.Parse(typeof(JobStatus), status);
            if (GlobalConfiguration.Current.LongTermStorage != null &&
                (jStatus == JobStatus.Processed || jStatus == JobStatus.Failed))
                GlobalConfiguration.Current.LongTermStorage.DeleteBackgroundJobs(ids);
            else
            {
                foreach(Guid id in ids)
                    GlobalConfiguration.Current.Storage.DeleteBackgroundJob(id);
            }
            return Json(new { });
        }

        [HttpPost]
        public IActionResult DeleteScheduledJobs(Guid[] ids)
        {
            foreach(Guid id in ids)
                GlobalConfiguration.Current.Storage.DeleteJob(id);
            return Json(new { });
        }

        [HttpPost]
        public IActionResult RetryJobs(Guid[] ids)
        {
            foreach(Guid id in ids)
                EnqueueIt.BackgroundJobs.ReEnqueue(id);
            return Json(new { });
        }

        [HttpPost]
        public IActionResult EnqueueJobs(Guid[] ids)
        {
            foreach(Guid id in ids)
                EnqueueIt.BackgroundJobs.EnqueueById(id);
            return Json(new { });
        }
        
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }

        public IActionResult Locks()
        {
            return View(GlobalConfiguration.Current.Storage.GetAllDistributedLocks());
        }

        public IActionResult AllKeys()
        {
            return View(GlobalConfiguration.Current.Storage.GetAllKeys());
        }
    }
}
