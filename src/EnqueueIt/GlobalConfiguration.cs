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
using EnqueueIt.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnqueueIt
{
    public class GlobalConfiguration
    {
        private static GlobalConfiguration current;
        public static GlobalConfiguration Current {
            get {
                if (current == null)
                    new GlobalConfiguration();
                return current;
            }
        }

        internal IServiceProvider ServiceProvider { get; set; }
        public GlobalConfiguration(ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider, IOptions<Configuration> config) : this()
        {
            ServiceProvider = serviceProvider;
            Configuration = config.Value;
            loggerFactory.ConfigureEnqueueIt();
        }

        private GlobalConfiguration() {
            Recur.Settings.DateTimeKind = DateTimeKind.Utc;
            current = this;
        }

        private string argument;
        public IStorage Storage { get; set; }
        public ILongTermStorage LongTermStorage { get; set; }
        public bool JobProcessing { get; private set; }

        private ILogger logger;
        public ILogger Logger {
            get {
                if (logger == null)
                    logger = LoggerFactory.Create(config => config.Services.AddLogging()).CreateLogger("EnqueueIt");
                return logger;
            }
            set { logger = value; } 
        }
        
        private Configuration configuration;
        public Configuration Configuration
        {
            get
            {
                if (configuration == null)
                {
                    configuration = new Configuration();
                    Configuration.LoadFromFile();
                }
                return configuration;
            }
            set { configuration = value; }
        }

        public GlobalConfiguration SetupEnqueueIt(string[] args)
        {
            if (args != null && args.Length > 0 && args.Any(arg => arg.StartsWith("EnqueueIt.Base64:")))
            {
                JobProcessing = true;
                argument = args.FirstOrDefault(arg => arg.StartsWith("EnqueueIt.Base64:"));
                if (argument != null)
                    argument = argument.Substring(17);
            }
            return current;
        }

        public void RunJob()
        {
            if (!string.IsNullOrWhiteSpace(argument))
                new JobExecution(argument).Start(false);
            Environment.Exit(0);
        }
    }
}