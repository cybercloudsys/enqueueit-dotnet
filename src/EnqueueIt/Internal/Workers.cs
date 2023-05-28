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

using System.Collections.Generic;

namespace EnqueueIt.Internal
{
    internal class Workers
    {
        int workers = 0;
        Dictionary<string, int> queueWorkers = new Dictionary<string, int>();

        internal void AddQueue(string queue)
        {
            lock (this)
            {
                if (!queueWorkers.ContainsKey(queue))
                    queueWorkers.Add(queue, 0);
            }
        }

        internal int TotalWorkers()
        {
            return workers;
        }

        internal int QueueWorkers(string queue)
        {
            return queueWorkers[queue];
        }

        internal void WorkerStarted(string queue)
        {
            workers++;
            lock (queueWorkers)
                queueWorkers[queue]++;
        }

        internal void WorkerDisposed(string queue)
        {
            workers--;
            lock (queueWorkers)
                queueWorkers[queue]--;
        }
    }
}