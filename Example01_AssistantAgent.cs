using AutoGen.Core;
using AutoGen.SemanticKernel.Extension;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AutoGen.BasicSamples;

public static class Example01_AssistantAgent
{
    public static async Task RunAsync(IKernelBuilder kernelBuilder, OpenAIPromptExecutionSettings settings)
    {
        // create assistant agent
        var assistantAgent = kernelBuilder.Build().ToSemanticKernelAgent(
                name: "assistant",
                systemMessage: "You convert what user said to all uppercase.",
                settings: settings)
            .RegisterMessageConnector()
            .RegisterPrintMessage();
        // talk to the assistant agent
        var reply = await assistantAgent.SendAsync("hello world");
        reply.Should().BeOfType<TextMessage>();
        reply.GetContent().Should().Be("HELLO WORLD");

        // to carry on the conversation, pass the previous conversation history to the next call
        var conversationHistory = new List<IMessage>
        {
            new TextMessage(Role.User, "hello world"), // first message
            reply, // reply from assistant agent
        };

        reply = await assistantAgent.SendAsync("hello world again", conversationHistory);
        reply.Should().BeOfType<TextMessage>();
        reply.GetContent().Should().Be("HELLO WORLD AGAIN");
    }
}