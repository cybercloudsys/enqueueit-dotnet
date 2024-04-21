# Enqueue It
[![Official Site](https://img.shields.io/badge/site-enqueueit.com-blue.svg)](https://www.enqueueit.com) [![Latest version](https://img.shields.io/badge/dynamic/xml?url=https%3A%2F%2Fgithub.com%2Fcybercloudsys%2Fenqueueit-dotnet%2Fraw%2Fmaster%2Fsrc%2FEnqueueIt%2FEnqueueIt.csproj&query=%2F%2FProject%2FPropertyGroup%2FVersion&prefix=v&label=release)](https://www.nuget.org/packages?q=enqueueit) [![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0) [![Build status](https://ci.appveyor.com/api/projects/status/9twyimm5686cvlfh/branch/master?svg=true)](https://ci.appveyor.com/project/abudaqqa/enqueueit/branch/master)

Easy and scalable solution for manage and execute background tasks seamlessly in .NET applications. It allows you to schedule, queue, and process your jobs and microservices efficiently.

Designed to support distributed systems, enabling you to scale your background processes across multiple servers. With advanced features like performance monitoring, exception logging, and integration with various storage types, you have complete control and visibility over your workflow.

Provides a user-friendly web dashboard that allows you to monitor and manage your jobs and microservices from a centralized location. You can easily check the status of your tasks, troubleshoot issues, and optimize performance.

## Benefits and Features
- Schedule and queue background jobs and microservices
- Run multiple servers for increased performance and reliability
- Monitor CPU and memory usage of microservices
- Log exceptions to help find bugs and memory leaks
- Connect to multiple storage types for optimal performance:
  - Main storage (Redis) for active jobs and services
  - Long-term storage (SQL databases such as SQL Server, PostgreSQL, MySQL, and more) for completed jobs and job history
- Web dashboard for monitoring jobs and microservices

## Getting Started
To get started with EnqueueIt, follow these steps:

- Install the storage package that matches your main storage server. For example, if you are using Redis, install [EnqueueIt.Redis](https://www.nuget.org/packages/EnqueueIt.Redis/).
- Install the long-term storage package that matches your SQL database. For example, if you are using SQL Server, install [EnqueueIt.SqlServer](https://www.nuget.org/packages/EnqueueIt.SqlServer/).
- Create a configuration file named enqueueIt.json in your project and specify the connection strings and settings for your storage servers and queues. Here is an example of a configuration file:
  ```
  {
    "StorageConfig": "localhost:6379",
    "StorageType": "Redis",
    "LongTermStorageConfig": "Server=localhost;Database=JobsDb;User ID=sa;Password=P@ssw0rd;",
    "LongTermStorageType": "SqlServer",
    "Servers": [
      {
        "Queues": [
          {
            "Name": "jobs",
            "WorkersCount": 50,
            "Retries": 0,
            "RetryDelay": 5
          },
          {
            "Name": "services",
            "WorkersCount": 50,
            "Retries": 0,
            "RetryDelay": 5
          }
        ]
      }
    ]
  }
  ```

- Load the configuration file and initialize the storage providers by calling the `Configure` method. The exact name of the method may vary based on the selected storage type. For example, if you are using Redis and SQL Server, you can call:
  ```
  GlobalConfiguration.Current.Configuration.LoadFromFile().UseRedisStorage().UseSqlServerStorage();
  ```
- If you are using a web application, you can also add EnqueueIt to your services by calling `AddEnqueueIt` and passing the same configuration method as above. For example:
  ```
  services.AddEnqueueIt(config => config.LoadFromFile().UseRedisStorage().UseSqlServerStorage());
  ```

- To start the server service that manages and executes enqueued/scheduled jobs, call `Servers.StartServer()`.

### Running Background Jobs
To run a background job, you can use the `BackgroundJobs.Enqueue` method and pass a delegate that represents the work to be done. For example, to print a message to the console, you can write:
  ```
  BackgroundJobs.Enqueue(() => Console.WriteLine("Easy Job!"))
  ```
  This will add the job to the default queue and it will be executed as soon as possible by the EnqueueIt.Server service.


### Scheduling Jobs
EnqueueIt allows you to schedule jobs to run at a specific time or after another job has completed. There are three types of scheduled jobs you can create with EnqueueIt:

- One-time job: This is a job that will run only once at a given time. You can use the `BackgroundJobs.Schedule` method and pass the delegate and the time as parameters. For example, to print a message to the console after 5 minutes, you can write:
  ```
  BackgroundJobs.Schedule(() => Console.WriteLine("Run this later"), DateTime.Now.AddMinutes(5))
  ```

- Recurring job: This is a job that will run repeatedly according to a specified frequency. You can use the `BackgroundJobs.Subscribe` method and pass the name, the delegate and the recurring pattern as parameters. The recurring pattern is an instance of the `RecurringPattern` class from [Recur](https://github.com/cybercloudsys/recur-dotnet) package that defines how often the job should run. For example, to print a message to the console every day at 06:00 AM, you can write:
  ```
  BackgroundJobs.Subscribe("My Daily Job", () => Console.WriteLine("Run this later"), RecurringPattern.Daily(6))
  ```

- Job dependent on another job: This is a job that will run only after another job has finished successfully. You can use the `BackgroundJobs.EnqueueAfter` method and pass the delegate and the ID of the previous job as parameters. The ID of a job is returned by the `BackgroundJobs.Enqueue`. For example, to print two messages to the console in sequence, you can write:
  ```
  //this is a background job
  string jobId = BackgroundJobs.Enqueue(() => Console.WriteLine("Easy Job!"));
  
  //this is another job to be run after the background job is being completed
  BackgroundJobs.EnqueueAfter(() => Console.WriteLine("Run this after the easy job!"), jobId);
  ```

## Using EnqueueIt for Microservices
EnqueueIt also supports running and scheduling microservices, which are small applications that perform a specific task. To use EnqueueIt for microservices, you need to do the following:

### Configuring Microservices
To configure your microservices, you need to add them to the `Applications` section of the enqueueIt.json file. For each microservice, you need to specify its name and base directory. The name should be unique and the base directory should be the path to the folder that contains the microservice executable file. For example, if you have two microservices named microservice1, you can add them to the configuration file like this:
  ```
  "Applications": [
    {
      "Name": "microservice1",
      "BaseDirectory": "Microservice1" // replace the value with the full path of the directory of microservice1
    },
    {
      "Name": "microservice2",
      "BaseDirectory": "Microservice2" // replace the value with the full path of the directory of microservice2
    }
  ],
  ```

### Running Microservices
To run a microservice, you can use the `Microservices.Enqueue` method and pass the name of the microservice and an object that represents the input for the microservice. The object will be serialized and passed as a command-line argument to the microservice executable file. For example, to run a microservice named microservice1 with an object that has a property called Message, you can write:
  ```
  Microservices.Enqueue("microservice1", new { Message = "Hello World" });
  ```
  This will add the microservice to the default queue and it will be executed as soon as possible by the EnqueueIt.Server service.

### Scheduling Microservices
EnqueueIt allows you to schedule microservices to run at a specific time or after another job has completed. There are three types of scheduled microservices you can create with EnqueueIt:

- One-time microservice: This is a microservice that will run only once at a given time. You can use the `Microservices.Schedule` method and pass the name of the microservice, the input object and the time as parameters. For example, to run a microservice named microservice1 with an object that has a property called Message after 5 minutes, you can write:
  ```
  Microservices.Schedule("microservice1", new { Message = "Run this later" }, DateTime.Now.AddMinutes(5))
  ```

- Recurring microservice: This is a microservice that will run repeatedly according to a specified frequency. You can use the `Microservices.Subscribe` method and pass the name of the microservice, the input object and the recurring pattern as parameters. The recurring pattern is an instance of the `RecurringPattern` class from [Recur](https://github.com/cybercloudsys/recur-dotnet) package that defines how often the microservice should run. For example, to run a microservice named microservice1 with an object that has a property called Message every day at 06:00 AM, you can write:
  ```
  Microservices.Subscribe("microservice1", new { Message = "Run this later" }, RecurringPattern.Daily(6))
  ```

- Microservice dependent on another job: This is a microservice that will run only after another job has finished successfully. You can use the `Microservices.EnqueueAfter` method and pass the name of the microservice, the input object and the ID of the previous job as parameters. The ID of a job is returned by the `BackgroundJobs.Enqueue` or `Microservices.Enqueue` methods. For example, to run a background job and then a microservice in sequence, you can write:
  ```
  //this is a background job
  string jobId = BackgroundJobs.Enqueue(() => Console.WriteLine("Easy Job!"));
  
  //this is a microservice to be run after the background job is being completed
  Microservices.EnqueueAfter("microservice1", new { Message = "Run this after the easy job!" }, jobId);
  ```

## Web Dashboard
EnqueueIt.Dashboard is a package that provides a web dashboard for monitoring and managing your background jobs and microservices. You can use the dashboard to view the status, details and history of your jobs, as well as to create, edit or delete them. The dashboard can be added to any ASP.NET application, regardless of whether it is the same application that runs your jobs or not. To use the web dashboard with default settings, follow these steps:

1. Install the EnqueueIt.Dashboard package from [NuGet](https://www.nuget.org/packages/EnqueueIt.Dashboard/).
2. In your ASP.NET application, add the `AddEnqueueItDashboard` method to your services configuration and the `UseEnqueueItDashboard` method to your app configuration. For example:

  ```
  builder.Services.AddEnqueueItDashboard();
  ...
  app.UseEnqueueItDashboard();
  ```
3. Run your ASP.NET application and navigate to http://localhost/EnqueueIt to access the dashboard.


### Securing the Dashboard
By default, the EnqueueIt.Dashboard web dashboard is accessible to all users of your application. However, if you need to restrict access to the dashboard to certain users or roles, you can use the AuthorizationPolicy object to define an authorization policy that controls who can access the dashboard.

To set an authorization policy, you can pass AuthorizationPolicy or use AuthorizationPolicyBuilder to the AddEnqueueItDashboard method, like this:

```
builder.Services.AddEnqueueItDashboard(policy => policy.RequireRole("administrator"));
```
In this example, we're using an authorization policy that requires authentication and the "Administrator" role to access the dashboard. You can customize this policy based on your specific requirements.

### Changing the Dashboard Path
By default, the EnqueueIt.Dashboard web dashboard is accessible at the "/EnqueueIt" URL path. However, if you need to use a different path for the dashboard, you can use the UseEnqueueItDashboard method with the routePrefix parameter to specify a custom path:
```
app.UseEnqueueItDashboard(routePrefix: "/dashboard");
```
In this example, we're using the "/dashboard" path for the dashboard instead of the default "/EnqueueIt". You can customize this value to match your desired URL structure.

## Links

- [Homepage](https://www.enqueueit.com)
- [Examples](https://github.com/cybercloudsys/enqueueit/tree/master/Examples)
- [NuGet Packages](https://www.nuget.org/profiles/CyberCloudSystems)
- [Go Package](https://pkg.go.dev/github.com/cybercloudsys/enqueueit-go)

## License

EnqueueIt\
Copyright © 2023 [Cyber Cloud Systems LLC](https://www.cybercloudsys.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.

Any user of this program who modifies its source code and distributes
the modified version must make the source code available to all
recipients of the software, under the terms of the license.

If you have any questions about this agreement, You can contact us
through this email info@cybercloudsys.com
