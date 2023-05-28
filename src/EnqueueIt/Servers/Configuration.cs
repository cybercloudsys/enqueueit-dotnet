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
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace EnqueueIt
{
    public class Configuration
    {
        public Configuration LoadFromFile()
        {
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "enqueueit.json");
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            string jsonEnvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"EnqueueIt.{env}.json");
            if (File.Exists(jsonEnvPath))
                PopulateConfig(jsonEnvPath);
            else if (File.Exists(jsonPath))
                PopulateConfig(jsonPath);
            return this;
        }

        private void PopulateConfig(string fileName)
        {
            var jsonOptions = new JsonSerializerOptions { AllowTrailingCommas = true,
                Converters = { new JsonStringEnumConverter()} };
            var config = JsonSerializer.Deserialize(File.ReadAllText(fileName), typeof(Configuration), jsonOptions);
            foreach(var prop in GetType().GetProperties())
                prop.SetValue(this, prop.GetValue(config));
        }

        public string StorageConfig { get; set; }
        public string LongTermStorageConfig { get; set; }
        public List<Application> Applications { get; set; }
        public List<Server> Servers { get; set; }
        public List<DayOfWeek> OffDays { get; set; }
        public StorageType StorageType { get; set; }
        public LongTermStorageType LongTermStorageType { get; set; }
        public bool EnableStopServers { get; set; }
        public bool EnableDeleteAll { get; set; }
        private int connectionRetries = 10;
        public int ConnectionRetries {
            get { return connectionRetries; }
            set { connectionRetries = resetByMinMax(value, 0, 30); }
        }
        private int connectionRetryInterval = 3;
        public int ConnectionRetryInterval {
            get { return connectionRetryInterval; }
            set { connectionRetryInterval = resetByMinMax(value, 2, 30); }
        }
        private int jobHeartbeatInterval = 1;
        public int JobHeartbeatInterval {
            get { return jobHeartbeatInterval; }
            set
            {
                jobHeartbeatInterval = resetByMinMax(value, 1, 30);
                inactiveJobTimeout = resetByMinMax(value, jobHeartbeatInterval + 4, 60);
            }
        }
        private int inactiveJobTimeout = 15;
        public int InactiveJobTimeout {
            get { return inactiveJobTimeout; }
            set { inactiveJobTimeout = resetByMinMax(value, jobHeartbeatInterval + 4, 60); }
        }
        private int serverHeartbeatInterval = 1;
        public int ServerHeartbeatInterval {
            get { return serverHeartbeatInterval; }
            set { serverHeartbeatInterval = resetByMinMax(value, 1, 30); }
        }
        private int inactiveServerTimeout = 15;
        public int InactiveServerTimeout {
            get { return inactiveServerTimeout; }
            set { inactiveServerTimeout = resetByMinMax(value, serverHeartbeatInterval + 4, 60); }
        }
        private int lockHeartbeatInterval = 1;
        public int LockHeartbeatInterval {
            get { return lockHeartbeatInterval; }
            set
            {
                lockHeartbeatInterval = resetByMinMax(value, 1, 30);
                inactiveLockTimeout = resetByMinMax(inactiveLockTimeout, lockHeartbeatInterval + 4, 60);
            }
        }
        private int inactiveLockTimeout = 15;
        public int InactiveLockTimeout {
            get { return inactiveLockTimeout; }
            set { inactiveLockTimeout = resetByMinMax(value, lockHeartbeatInterval + 4, 60); }
        }
        private int storageExpirationInDays = 30;
        public int StorageExpirationInDays {
            get { return storageExpirationInDays; }
            set { storageExpirationInDays = resetByMinMax(value, 1, 730); }
        }
        private int storageSyncInterval = 1;
        public int StorageSyncInterval {
            get { return storageSyncInterval; }
            set { storageSyncInterval = resetByMinMax(value, 1, 30); }
        }
        private int storageSyncBatchSize = 10000;
        public int StorageSyncBatchSize {
            get { return storageSyncBatchSize; }
            set { storageSyncBatchSize = resetByMinMax(value, 500, 10000); }
        }
        private int cleanStorageInterval = 60;
        public int CleanStorageInterval {
            get { return cleanStorageInterval; }
            set { cleanStorageInterval = resetByMinMax(value, 30, 1800); }
        }

        private int resetByMinMax(int value, int min, int max)
        {
            if (value < min)
                return min;
            else if (value > max)
                return max;
            else
                return value;
        }
    }
}