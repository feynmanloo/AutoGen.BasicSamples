// See https://aka.ms/new-console-template for more information

using AutoGen.BasicSamples;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

Console.WriteLine("Hello, World!");
var settings = new OpenAIPromptExecutionSettings
{
    Temperature = 0
};
var kernelBuilder = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId: GlobalConfiguration.modelId, apiKey: GlobalConfiguration.openAIKey, httpClient: new HttpClient(new OpenAiHttpClientHandler(GlobalConfiguration.url)));

// await Example01_AssistantAgent.RunAsync(kernelBuilder, settings);
// await Example02_TwoAgent_MathChat.RunAsync(kernelBuilder, settings);
// await Example03_Agent_FunctionCall.RunAsync(kernelBuilder, settings);
await Example04_Dynamic_GroupChat_Coding_Task.RunAsync(kernelBuilder, settings);