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

namespace EnqueueIt
{
    public class JobError
    {
        public JobError() { }
        public JobError(Exception exception)
        {
            if (exception != null)
            {
                Message = exception.Message;
                StackTrace = exception.StackTrace;
                if (exception.InnerException != null)
                    InnerError = new JobError(exception.InnerException);
            }
            else
                Message = "Unknow exception";
        }

        public JobError(StreamReader error)
        {
            if (error != null)
            {
                Message = error.ReadLine();
                if (!string.IsNullOrWhiteSpace(Message) && Message.StartsWith("Unhandled exception. "))
                    Message = Message.Substring(21);
                StackTrace = error.ReadToEnd().Trim();
            }
            else
                Message = "Unknow error";
        }

        public string Message { get; set; }
        public string StackTrace { get; set; }
        public JobError InnerError { get; set; }
    }
}