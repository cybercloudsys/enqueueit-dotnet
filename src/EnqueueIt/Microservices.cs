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
using System.Linq.Expressions;
using System.Threading.Tasks;
using Recur;

namespace EnqueueIt
{
    /// <summary>
    /// Provide static methods to schedule and enqueue microservices
    /// </summary>
    public static class Microservices
    {
        /// <summary>
        /// Creates microservice and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="appName">The app file name in which to be executed when the microservice started.</param>
        /// <param name="startAt">The time for microservice to be enqueued.</param>
        /// <param name="queue">The queue name.</param>
        /// <returns>Job id.</returns>
        public static Guid Schedule(string appName, object argument, DateTime startAt, string queue = "services")
        {
            return Schedule(null, appName, argument, startAt, null, null, queue);
        }

        /// <summary>
        /// Creates or updates a recurring microservice and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring microservice to be created or updated if a job with the same name was created previously.</param>
        /// <param name="appName">The app file name in which to be executed when the microservice started.</param>
        /// <param name="recurringPattern">the recurring pattern of the microservice.</param>
        /// <param name="queue">The queue name.</param>
        public static Guid Subscribe(string uniqeName, string appName, object argument,
            RecurringPattern recurringPattern, string queue = "services")
        {
            return Schedule(uniqeName, appName, argument, null, recurringPattern, null, queue);
        }

        private static Job CreateMicroservice(string name, string appName, object argument, DateTime? startAt,
            RecurringPattern recurringPattern, Guid? backgroundJobId, string queue)
        {
            Job job = new Job();
            job.Id = Guid.NewGuid();
            job.CreatedAt = DateTime.UtcNow;
            job.Name = name;
            job.Queue = queue;
            job.Tries = 0;
            job.IsRecurring = recurringPattern != null;
            job.StartAt = startAt.HasValue ? startAt.Value.ToUniversalTime() : startAt;
            job.Active = true;
            job.RecurringPattern = recurringPattern;
            if (backgroundJobId.HasValue)
                job.AfterBackgroundJobIds = backgroundJobId.ToString();
            job.AppName = appName;
            if (argument != null)
            {
                Type type = argument.GetType();
                if (type.IsValueType || type == typeof(string))
                    job.Argument = argument.ToString();
                else
                    job.Argument = Serializer.Serialize(argument);
            }
            job.Type = JobType.Microservice;
            return job;
        }

        private static Guid Schedule(string name, string appName, object argument, DateTime? startAt,
            RecurringPattern recurringPattern, Guid? backgroundJobId, string queue)
        {
            Job job = CreateMicroservice(name, appName, argument, startAt, recurringPattern, backgroundJobId, queue);
            GlobalConfiguration.Current.Storage.SaveJob(job, true);
            return job.Id;
        }

        /// <summary>
        /// Start new microservice and enqueue it based on previously scheduled microservice
        /// </summary>
        /// <param name="jobId">The job id.</param>
        /// <returns>Background job id.</returns>
        public static Guid? EnqueueById(Guid jobId)
        {
            return BackgroundJobs.EnqueueById(jobId);
        }

        /// <summary>
        /// Creates microservice to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="appName">The app file name in which to be executed when the microservice started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <param name="queue">The queue name.</param>
        /// <returns>Job id.</returns>
        public static Guid EnqueueAfter(string appName, object argument, Guid backgroundJobId, string queue = "services")
        {
            var jobId = Schedule(null, appName, argument, null, null, backgroundJobId, queue);
            GlobalConfiguration.Current.Storage.EnqueueAfter(jobId, backgroundJobId);
            return jobId;
        }

        /// <summary>
        /// Creates microservice and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <remarks>
        /// In order to the microservice to be started an EnqueuIt server must be running
        /// the same queue name of the job queue. also queue workers must be less than
        /// the maximum limit of number of workers configured on the server
        /// </remarks>
        /// <param name="appName">The app file name in which to be executed when the microservice started.</param>
        /// <param name="queue">The queue name.</param>
        /// <returns>Background job id.</returns>
        public static Guid Enqueue(string appName, object argument, string queue = "services")
        {
            BackgroundJob bgJob = new BackgroundJob();
            bgJob.Id = Guid.NewGuid();
            bgJob.Job = CreateMicroservice(null, appName, argument, null, null, null, queue);
            bgJob.JobId = bgJob.Job.Id;
            bgJob.CreatedAt = bgJob.Job.CreatedAt;
            bgJob.Status = JobStatus.Enqueued;
            GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
            return bgJob.Id;
        }

        /// <summary>
        /// Creates new microservice and enqueue it based previously enqueued microservice
        /// </summary>
        /// <param name="backgroundJobId">The old background job id.</param>
        /// <returns>The new background job id.</returns>
        public static Guid? ReEnqueue(Guid backgroundJobId)
        {
            return BackgroundJobs.ReEnqueue(backgroundJobId);
        }

        /// <summary>
        /// Stop microservice if the microservice is running or still enqueued
        /// </summary>
        /// <param name="backgroundJobId">The background job id.</param>
        public static void Stop(Guid backgroundJobId)
        {
            BackgroundJobs.Stop(backgroundJobId);
        }

        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        /// <returns>Job id.</returns>
        public static Guid Schedule(Expression<Action> methodCall, DateTime startAt)
        {
            return BackgroundJobs.Schedule(null, methodCall.Body as MethodCallExpression,
                startAt, null, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Schedule(Expression<Func<Task>> methodCall, DateTime startAt)
        {
            return BackgroundJobs.Schedule(null, methodCall.Body as MethodCallExpression,
                startAt, null, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Schedule<T>(Expression<Func<T, Task>> methodCall, DateTime startAt)
        {
            return BackgroundJobs.Schedule(null, methodCall.Body as MethodCallExpression,
                startAt, null, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Schedule<T>(Expression<Action<T>> methodCall, DateTime startAt)
        {
            return BackgroundJobs.Schedule(null, methodCall.Body as MethodCallExpression,
                startAt, null, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Subscribe(string uniqeName, Expression<Action> methodCall,
            RecurringPattern recurringPattern)
        {
            return BackgroundJobs.Schedule(uniqeName, methodCall.Body as MethodCallExpression,
                null, recurringPattern, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Subscribe(string uniqeName, Expression<Func<Task>> methodCall,
            RecurringPattern recurringPattern)
        {
            return BackgroundJobs.Schedule(uniqeName, methodCall.Body as MethodCallExpression,
                null, recurringPattern, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Subscribe<T>(string uniqeName, Expression<Func<T, Task>> methodCall,
            RecurringPattern recurringPattern)
        {
            return BackgroundJobs.Schedule(uniqeName, methodCall.Body as MethodCallExpression,
                null, recurringPattern, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Subscribe<T>(string uniqeName, Expression<Action<T>> methodCall,
            RecurringPattern recurringPattern)
        {
            return BackgroundJobs.Schedule(uniqeName, methodCall.Body as MethodCallExpression,
                null, recurringPattern, null, JobType.Microservice);
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        /// <returns>Job id.</returns>
        public static Guid EnqueueAfter(Expression<Action> methodCall, Guid backgroundJobId)
        {
            return BackgroundJobs.EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Microservice);
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid EnqueueAfter(Expression<Func<Task>> methodCall, Guid backgroundJobId)
        {
            return BackgroundJobs.EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Microservice);
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid EnqueueAfter<T>(Expression<Func<T, Task>> methodCall, Guid backgroundJobId)
        {
            return BackgroundJobs.EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Microservice);
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid EnqueueAfter<T>(Expression<Action<T>> methodCall, Guid backgroundJobId)
        {
            return BackgroundJobs.EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Microservice);
        }

        /// <summary>
        /// Creates backgorund job and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <remarks>
        /// In order to the background job to be started an EnqueuIt server must be running
        /// the same queue name of the job queue. also queue workers must be less than
        /// the maximum limit of number of workers configured on the server
        /// </remarks>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        /// <returns>Background job id.</returns>
        public static Guid Enqueue(Expression<Action> methodCall)
        {
            return BackgroundJobs.Enqueue(methodCall.Body as MethodCallExpression, JobType.Microservice);
        }

        /// <summary>
        /// Creates backgorund job and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Enqueue(Expression<Func<Task>> methodCall)
        {
            return BackgroundJobs.Enqueue(((methodCall.Body as NewExpression).Arguments[0] as Expression<Action>)
                .Body as MethodCallExpression, JobType.Microservice);
        }

        /// <summary>
        /// Creates backgorund job and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Enqueue<T>(Expression<Func<T, Task>> methodCall)
        {
            return BackgroundJobs.Enqueue(methodCall.Body as MethodCallExpression, JobType.Microservice);
        }

        /// <summary>
        /// Creates backgorund job and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "services" if no queue name provided</remarks>
        public static Guid Enqueue<T>(Expression<Action<T>> methodCall)
        {
            return BackgroundJobs.Enqueue(methodCall.Body as MethodCallExpression, JobType.Microservice);
        }
    }
}