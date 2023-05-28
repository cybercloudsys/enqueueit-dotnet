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

namespace EnqueueIt
{
    public class Server
    {
        public Guid Id { get; set; }
        public string Hostname { get; set; }
        public List<Queue> Queues { get; set; }
        public ServerStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public bool HasDataSync { get; set; }
        public int WorkersCount { get; set; } = 50;
    }
}