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
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Recur;

namespace EnqueueIt
{
    /// <summary>
    /// Provide static methods to schedule, enqueue, re-enqueue and stop background jobs
    /// </summary>
    public static class BackgroundJobs
    {
        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        /// <returns>Job id.</returns>
        public static Guid Schedule(Expression<Action> methodCall, DateTime startAt)
        {
            return Schedule(null, methodCall.Body as MethodCallExpression, startAt, null, null, JobType.Thread);
        }

        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Schedule(Expression<Func<Task>> methodCall, DateTime startAt)
        {
            return Schedule(null, methodCall.Body as MethodCallExpression, startAt, null, null, JobType.Thread);
        }

        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Schedule<T>(Expression<Func<T, Task>> methodCall, DateTime startAt)
        {
            return Schedule(null, methodCall.Body as MethodCallExpression, startAt, null, null, JobType.Thread);
        }

        /// <summary>
        /// Creates job and schedule it to be enqueued on giving time.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="startAt">The time for background job to be enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Schedule<T>(Expression<Action<T>> methodCall, DateTime startAt)
        {
            return Schedule(null, methodCall.Body as MethodCallExpression, startAt, null, null, JobType.Thread);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Subscribe(string uniqeName, Expression<Action> methodCall,
            RecurringPattern recurringPattern)
        {
            return Schedule(uniqeName, methodCall.Body as MethodCallExpression, null, recurringPattern, null, JobType.Thread);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Subscribe(string uniqeName, Expression<Func<Task>> methodCall,
            RecurringPattern recurringPattern)
        {
            return Schedule(uniqeName, methodCall.Body as MethodCallExpression, null, recurringPattern, null, JobType.Thread);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Subscribe<T>(string uniqeName, Expression<Func<T, Task>> methodCall,
            RecurringPattern recurringPattern)
        {
            return Schedule(uniqeName, methodCall.Body as MethodCallExpression, null,
                recurringPattern, null, JobType.Thread);
        }

        /// <summary>
        /// Creates or updates a recurring job and schedule it to be enqueued on a giving pattern
        /// </summary>
        /// <param name="uniqeName">The unique job name is required for recurring job to be created or updated if a job with the same name was created previously.</param>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="recurringPattern">the recurring pattern of the background job.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Subscribe<T>(string uniqeName, Expression<Action<T>> methodCall,
            RecurringPattern recurringPattern)
        {
            return Schedule(uniqeName, methodCall.Body as MethodCallExpression, null,
                recurringPattern, null, JobType.Thread);
        }

        internal static Job CreateJob(string name, MethodCallExpression methodCall, DateTime? startAt,
            RecurringPattern recurringPattern, Guid? backgroundJobId, JobType jobType)
        {
            if (!methodCall.Method.IsPublic)
                throw new NotPublicMethodException();
            Job job = new Job();
            job.Id = Guid.NewGuid();
            job.CreatedAt = DateTime.UtcNow;
            job.Name = name;
            job.Queue = GetQueue(methodCall.Method) ?? GetQueue(methodCall.Method.DeclaringType)
                ?? (jobType == JobType.Thread ? "jobs" : "services");
            job.Tries = 0;
            job.IsRecurring = recurringPattern != null;
            job.StartAt = startAt.HasValue ? startAt.Value.ToUniversalTime() : startAt;
            job.Active = true;
            job.RecurringPattern = recurringPattern;
            if (backgroundJobId.HasValue)
                job.AfterBackgroundJobIds = backgroundJobId.ToString();
            var objType = methodCall.Method.DeclaringType;
            job.AppName = AppDomain.CurrentDomain.FriendlyName + ".dll";
            job.Type = jobType;
            job.JobArgument = new JobArgument();
            job.JobArgument.Assembly = objType.Assembly.FullName;
            job.JobArgument.ClassType = objType.AssemblyQualifiedName;
            job.JobArgument.MethodName = methodCall.Method.Name;
            job.JobArgument.MetadataToken = methodCall.Method.MetadataToken;
            if (methodCall.Method.IsStatic)
                job.JobArgument.Type = JobArgumentType.StaticMethod;
            else
            {
                var constructors = objType.GetConstructors();
                if (constructors.Length > 0)
                {
                    job.JobArgument.Type = JobArgumentType.NoParameterlessConstructor;
                    foreach(var constr in constructors)
                    {
                        if (constr.GetParameters().Length == 0)
                        {
                            job.JobArgument.Type = JobArgumentType.Normal;
                            break;
                        }
                    }
                }
            }
            var methodParams = methodCall.Method.GetParameters();
            job.JobArgument.Arguments = new List<Argument>();
            for (int i = 0; i < methodCall.Arguments.Count; i++)
            {
                var arg = methodCall.Arguments[i];
                var param = methodParams[i];
                Argument argument = new Argument {
                    Name = param.Name,
                    Type = arg.Type.AssemblyQualifiedName };
                
                if (arg is ConstantExpression)
                {
                    var value = (arg as ConstantExpression).Value;
                    if (value != null)
                        argument.Value = value.ToString();
                }
                else
                {
                    if (arg.Type.IsValueType || arg.Type == typeof(string))
                        argument.Value = Expression.Lambda(Expression.Convert(arg, arg.Type)).Compile().DynamicInvoke().ToString();
                    else if (arg.Type != typeof(JobCancellation))
                        argument.Value = Serializer.Serialize(Expression.Lambda(Expression.Convert(arg, arg.Type)).Compile().DynamicInvoke(), arg.Type);
                }
                job.JobArgument.Arguments.Add(argument);
            }
            return job;
        }

        private static string GetQueue(MemberInfo mInfo)
        {
            foreach (var attr in mInfo.CustomAttributes)
            {
                if (attr.AttributeType == typeof(QueueAttribute))
                {
                    if (attr.ConstructorArguments.Count == 1)
                        return attr.ConstructorArguments[0].Value.ToString();
                    break;
                }
            }
            return null;
        }

        internal static Guid Schedule(string name, MethodCallExpression methodCall, DateTime? startAt,
            RecurringPattern recurringPattern, Guid? backgroundJobId, JobType jobType)
        {
            Job job = CreateJob(name, methodCall, startAt, recurringPattern, backgroundJobId, jobType);
            GlobalConfiguration.Current.Storage.SaveJob(job, true);
            return job.Id;
        }

        /// <summary>
        /// Creates new background job and enqueue it based on previously scheduled job
        /// </summary>
        /// <param name="jobId">The job id.</param>
        /// <returns>Background job id.</returns>
        public static Guid? EnqueueById(Guid jobId)
        {
            var job = GlobalConfiguration.Current.Storage.GetJob(jobId);
            if (job != null)
            {
                if (!job.IsRecurring)
                {
                    job.Active = false;
                    GlobalConfiguration.Current.Storage.JobEnqueued(job.Id, job.Queue);
                }
                BackgroundJob bgJob = new BackgroundJob();
                bgJob.JobId = job.Id;
                bgJob.Id = Guid.NewGuid();
                bgJob.CreatedAt = DateTime.UtcNow;
                bgJob.Status = JobStatus.Enqueued;
                bgJob.Job = job;
                GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                return bgJob.Id;
            }
            return null;
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        /// <returns>Job id.</returns>
        public static Guid EnqueueAfter(Expression<Action> methodCall, Guid backgroundJobId)
        {
            return EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Thread);
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid EnqueueAfter(Expression<Func<Task>> methodCall, Guid backgroundJobId)
        {
            return EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Thread);
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid EnqueueAfter<T>(Expression<Func<T, Task>> methodCall, Guid backgroundJobId)
        {
            return EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Thread);
        }

        /// <summary>
        /// Creates job to be enqueued after another background job is completely procecced.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <param name="backgroundJobId">The other background job id that is currenlty enqueued.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid EnqueueAfter<T>(Expression<Action<T>> methodCall, Guid backgroundJobId)
        {
            return EnqueueAfter(methodCall.Body as MethodCallExpression, backgroundJobId, JobType.Thread);
        }

        internal static Guid EnqueueAfter(MethodCallExpression methodCall, Guid backgroundJobId, JobType jobType)
        {
            var jobId = Schedule(null, methodCall, null, null, backgroundJobId, jobType);
            GlobalConfiguration.Current.Storage.EnqueueAfter(jobId, backgroundJobId);
            return jobId;
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
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        /// <returns>Background job id.</returns>
        public static Guid Enqueue(Expression<Action> methodCall)
        {
            return Enqueue(methodCall.Body as MethodCallExpression, JobType.Thread);
        }

        /// <summary>
        /// Creates backgorund job and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Enqueue(Expression<Func<Task>> methodCall)
        {
            return Enqueue(((methodCall.Body as NewExpression).Arguments[0] as Expression<Action>)
                .Body as MethodCallExpression, JobType.Thread);
        }

        /// <summary>
        /// Creates backgorund job and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Enqueue<T>(Expression<Func<T, Task>> methodCall)
        {
            return Enqueue(methodCall.Body as MethodCallExpression, JobType.Thread);
        }

        /// <summary>
        /// Creates backgorund job and enqueue it to be ready to be started as soon as posible.
        /// </summary>
        /// <param name="methodCall">The method call expression in which to be called when the background job started.</param>
        /// <remarks>the queue name can be set by Queue attribute, the default queue name is "jobs" if no queue name provided</remarks>
        public static Guid Enqueue<T>(Expression<Action<T>> methodCall)
        {
            return Enqueue(methodCall.Body as MethodCallExpression, JobType.Thread);
        }

        internal static Guid Enqueue(MethodCallExpression methodCall, JobType jobType)
        {
            Job job = new Job();
            BackgroundJob bgJob = new BackgroundJob();
            bgJob.Id = Guid.NewGuid();
            bgJob.Job = CreateJob(null, methodCall, null, null, null, jobType);
            bgJob.CreatedAt = bgJob.Job.CreatedAt;
            bgJob.JobId = bgJob.Job.Id;
            bgJob.Status = JobStatus.Enqueued;
            GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
            return bgJob.Id;
        }

        /// <summary>
        /// Creates new background job and enqueue it based previously enqueued backgorund job
        /// </summary>
        /// <param name="backgroundJobId">The old background job id.</param>
        /// <returns>The new background job id.</returns>
        public static Guid? ReEnqueue(Guid backgroundJobId)
        {
            var job = GlobalConfiguration.Current.Storage.GetBackgroundJob(backgroundJobId);
            if (job == null)
                job = GlobalConfiguration.Current.LongTermStorage.GetBackgroundJob(backgroundJobId);
            if (job != null)
            {
                BackgroundJob bgJob;
                if (job.Status == JobStatus.Interrupted || job.Status == JobStatus.Canceled)
                {
                    bgJob = job;
                    job.Status = JobStatus.Enqueued;
                    GlobalConfiguration.Current.Storage.SaveBackgroundJob(job);
                }
                else
                {
                    bgJob = new BackgroundJob();
                    bgJob.JobId = job.JobId;
                    bgJob.Id = Guid.NewGuid();
                    bgJob.Job = job.Job;
                    bgJob.CreatedAt = DateTime.UtcNow;
                    bgJob.Status = JobStatus.Enqueued;
                    GlobalConfiguration.Current.Storage.SaveBackgroundJob(bgJob);
                }
                return bgJob.Id;
            }
            return null;
        }

        /// <summary>
        /// Stop background job if the job is running or still enqueued
        /// </summary>
        /// <param name="backgroundJobId">The background job id.</param>
        public static void Stop(Guid backgroundJobId)
        {
            var job = GlobalConfiguration.Current.Storage.GetBackgroundJob(backgroundJobId);
            if (job != null && (job.Status == JobStatus.Enqueued || job.Status == JobStatus.Processing))
            {
                job.Status = JobStatus.Canceled;
                job.CompletedAt = DateTime.UtcNow;
                GlobalConfiguration.Current.Storage.SaveBackgroundJob(job);
            }
        }
    }
}