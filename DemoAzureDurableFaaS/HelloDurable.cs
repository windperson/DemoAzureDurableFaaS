using System.Collections.Generic;
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

        [FunctionName("HelloDurable_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            ILogger log)
        {
            var logger = CreateSerilogLogger(log);

            // Function input comes from the req`uest content.
            string instanceId = await starter.StartNewAsync(OrchestratorFuncName, null);

            logger.Information($"Started orchestration with ID = '{instanceId}'.");

            var durableOrchestrationStatus = await starter.GetStatusAsync(instanceId);

            while (durableOrchestrationStatus.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
            {
                logger.Information($"the {OrchestratorFuncName}() is still running");
                await Task.Delay(200);
                durableOrchestrationStatus = await starter.GetStatusAsync(instanceId);
            }

            var output = durableOrchestrationStatus.Output;
            logger.Information($" {ActivityFuncName}() completed, return= {output}");

            return starter.CreateCheckStatusResponse(req, instanceId);
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

        [FunctionName("HelloDurable_Hello")]
        public static HelloDto SayHello([ActivityTrigger] DurableActivityContext activityContext, ILogger log)
        {
            var logger = CreateSerilogLogger(log);
            var id = activityContext.InstanceId;

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
        

        private static Serilog.ILogger CreateSerilogLogger(ILogger log)
        {
            return new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.ILogger(log)
                .CreateLogger();
        }
    }
}