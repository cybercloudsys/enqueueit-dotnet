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
using System.Threading;
using System.Threading.Tasks;

namespace EnqueueIt
{
    public class DistributedLock : IDisposable
    {
        public DistributedLock(string key, bool start = true)
        {
            Key = key;
            if (start)
                Enter();
        }

        public string Id { get; set; }
        public string Key { get; set; }
        private DistributedLockItem lockItem;
        private bool isAlive = true;
        private Thread mainThread;

        private void Initialize()
        {
            lockItem = new DistributedLockItem();
            lockItem.Id = $"{Guid.NewGuid()}:{Key}";
            lockItem.Key = Key;
            lockItem.StartedAt = lockItem.LastActivity = DateTime.UtcNow;
            GlobalConfiguration.Current.Storage.SaveDistributedLock(lockItem);
        }

        private bool Enter(TimeSpan? timeout)
        {
            mainThread = Thread.CurrentThread;
            DateTime started = DateTime.UtcNow;
            Initialize();
            TimeSpan waitTime = TimeSpan.FromSeconds(GlobalConfiguration.Current.Configuration.LockHeartbeatInterval);
            while (true)
            {
                if (GlobalConfiguration.Current.Storage.IsDistributedLockEntered(Key, lockItem.Id))
                {
                    Started();
                    break;
                }
                else
                {
                    if (timeout.HasValue && timeout.Value.Ticks > 0 && DateTime.UtcNow - started >= timeout)
                        return false;
                    Alive();
                    Task.Delay(waitTime).Wait();
                }
            }
            return true;
        }

        public void Enter()
        {
            Enter(null);
        }

        public bool TryEnter()
        {
            return Enter(TimeSpan.Zero);
        }

        public bool TryEnter(int millisecondsTimeout)
        {
            return Enter(TimeSpan.FromMilliseconds(millisecondsTimeout));
        }

        public bool TryEnter(TimeSpan timeout)
        {
            return Enter(timeout);
        }

        private void Alive()
        {
            lock (this)
            {
                if (isAlive && mainThread.IsAlive)
                {
                    lockItem.LastActivity = DateTime.UtcNow;
                    GlobalConfiguration.Current.Storage.SaveDistributedLock(lockItem);
                }
            }
        }

        private async void Started()
        {
            Id = lockItem.Id;
            TimeSpan waitTime = TimeSpan.FromSeconds(GlobalConfiguration.Current.Configuration.LockHeartbeatInterval);
            while (isAlive)
            {
                Alive();
                await Task.Delay(waitTime);
            }
        }

        public void Dispose()
        {
            isAlive = false;
            lock (this)
                GlobalConfiguration.Current.Storage.DeleteDistributedLock(Key, Id);
        }
    }
}