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
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace EnqueueIt.Internal
{
    internal class JobExecution
    {
        CancellationTokenSource source;
        JobArgument jobArgument;
        List<object> jobArgs;
        DateTime? canceledAt;
        internal Thread Thread { get; set; }
        internal JobError Error { get; private set; }
        
        internal JobExecution(Guid bgJobId)
        {
            var bgJob = GlobalConfiguration.Current.Storage.GetBackgroundJob((Guid)bgJobId);
            if (bgJob != null && bgJob.Job != null)
                jobArgument = bgJob.Job.JobArgument;
        }

        internal JobExecution(string arg)
        {
            if (!string.IsNullOrWhiteSpace(arg))
                jobArgument = JsonSerializer.Deserialize<JobArgument>(Encoding.UTF8.GetString(Convert.FromBase64String(arg)));
        }
        
        internal void Start(bool async)
        {
            LoadArguments();
            if (async)
                ExecuteAsync();
            else
                Execute();
        }

        private void LoadArguments()
        {
            jobArgs = new List<object>();
            foreach (var arg in jobArgument.Arguments)
                if (arg != null)
                    jobArgs.Add(GetArgumentValue(arg));
        }

        private void ExecuteAsync()
        {
            Thread = new Thread(() => {
                try
                {
                    Execute();
                }
                catch (Exception ex)
                {
                    Error = new JobError(ex.InnerException ?? ex);
                }
                GC.Collect();
            });
            Thread.Start();
        }

        private void Execute()
        {
            List<string> list = new List<string>();
            Type classType = Type.GetType(jobArgument.ClassType);
            object obj = null;
            IServiceScope scope = null;
            if (jobArgument.Type != JobArgumentType.StaticMethod)
            {                  
                if (jobArgument.Type == JobArgumentType.NoParameterlessConstructor
                    && GlobalConfiguration.Current.ServiceProvider != null)
                {
                    scope = GlobalConfiguration.Current.ServiceProvider.CreateScope();
                    obj = scope.ServiceProvider.GetService(classType);
                }
                if (obj == null)
                    obj = Activator.CreateInstance(classType);
            }
            classType.GetMethods().First(m => m.MetadataToken == jobArgument.MetadataToken)
                .Invoke(obj, jobArgs.ToArray());
            if (scope != null)
                scope.Dispose();
        }

        private object GetArgumentValue(Argument arg)
        {
            Type argType = Type.GetType(arg.Type);
            if (argType == typeof(JobCancellation))
            {
                source = new CancellationTokenSource();
                return new JobCancellation { Token = source.Token };
            }
            else if (arg.Value != null)
            {
                if (argType.IsValueType || argType == typeof(string))
                {
                    if (argType.IsEnum)
                        return Enum.Parse(argType, arg.Value);
                    else
                        return Convert.ChangeType(arg.Value, Nullable.GetUnderlyingType(argType) ?? argType);
                }
                else
                    return JsonSerializer.Deserialize(arg.Value, argType);
            }
            else
                return null;
        }

        internal bool Stop()
        {
            if (source != null)
            {
                if (!canceledAt.HasValue)
                {
                    source.Cancel();
                    canceledAt = DateTime.UtcNow;
                }
                else if ((DateTime.UtcNow - canceledAt.Value).TotalSeconds >= GlobalConfiguration
                    .Current.Configuration.InactiveJobTimeout)
                {
                    Thread.Interrupt();
                    return true;
                }
            }
            else
            {
                Thread.Interrupt();
                return true;
            }
            return false;
        }
    }
}