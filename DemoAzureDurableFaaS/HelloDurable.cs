using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.ILogger;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DemoAzureDurableFaaS
{
    public static class HelloDurable
    {
        private const string OrchestratorFuncName = "HelloDurable";
        private const string ActivityFuncName = "HelloDurable_Hello";

        private const string InstanceId = "MySingletonInstanceId";

        [FunctionName("HelloDurable_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [OrchestrationClient] DurableOrchestrationClient starter,
            ILogger log)
        {
            var logger = CreateLogger(log);

            var existedInstanceStatus = await starter.GetStatusAsync(InstanceId);
            if (IsInstanceOccupied(existedInstanceStatus))
            {
                return req.CreateErrorResponse(HttpStatusCode.Conflict,
                        $"An instance with ID '{InstanceId}' is already running or scheduled to run now");
            }

            await starter.StartNewAsync(OrchestratorFuncName, InstanceId, null);

            logger.Information($"Started orchestration with ID = '{InstanceId}'.");

            var durableOrchestrationStatus = await starter.GetStatusAsync(InstanceId);

            while (durableOrchestrationStatus.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
            {
                logger.Information($"the {OrchestratorFuncName}() is still running");
                await Task.Delay(1000);
                durableOrchestrationStatus = await starter.GetStatusAsync(InstanceId);
            }

            var output = durableOrchestrationStatus.Output;
            logger.Information($" {ActivityFuncName}() completed, return= {output}");

            return starter.CreateCheckStatusResponse(req, InstanceId);
        }

        private static bool IsInstanceOccupied(DurableOrchestrationStatus instanceStatus)
        {
            return instanceStatus != null
                   && (instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.Running
                       || instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.Pending
                       || instanceStatus.RuntimeStatus == OrchestrationRuntimeStatus.ContinuedAsNew);
        }

        [FunctionName(OrchestratorFuncName)]
        public static async Task<List<HelloDto>> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context,
            ILogger log)
        {
            var logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.ILogger(log)
                .CreateLogger();

            logger.Information("In Orchestration method");

            var inputs = new List<InputDto>
            {
                new InputDto{ CityName = "Tokyo"},
                new InputDto{ CityName = "Seattle"},
                new InputDto{ CityName = "London"}
            };
            var outputs = new List<HelloDto>();

            foreach (var input in inputs)
            {
                logger.Information($"call Activity:{ActivityFuncName} with input={{@input}}", input);
                outputs.Add(await context.CallActivityAsync<HelloDto>(ActivityFuncName, input));
            }

            return outputs;
        }

        [FunctionName(ActivityFuncName)]
        public static HelloDto SayHello([ActivityTrigger] DurableActivityContext activityContext, ILogger log)
        {
            var logger = CreateLogger(log);
            var id = $"{activityContext.InstanceId}_{DateTime.UtcNow:O}";

            var input = activityContext.GetInput<InputDto>();

            logger.Information($"Saying hello to {{@input}}.", input);

            var output = new HelloDto
            {
                Id = id,
                Name = input.CityName,
                Message = $"Hello from {input.CityName}!"
            };

            return output;
        }


        private static Serilog.ILogger CreateLogger(ILogger log)
        {
            return new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.ILogger(log)
                .CreateLogger();
        }
    }
}