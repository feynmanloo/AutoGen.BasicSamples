using System.ComponentModel;
using AutoGen.Core;
using AutoGen.SemanticKernel.Extension;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AutoGen.BasicSamples;

public partial class Example03_Agent_FunctionCall
{
    /// <summary>
    /// upper case the message when asked.
    /// </summary>
    /// <param name="message"></param>
    [KernelFunction]
    [Description("upper case the message when asked.")]
    public async Task<string> UpperCase(string message)
    {
        return message.ToUpper();
    }

    /// <summary>
    /// Concatenate strings.
    /// </summary>
    /// <param name="strings">strings to concatenate</param>
    [KernelFunction]
    [Description("Concatenate strings.")]
    public async Task<string> ConcatString(string[] strings)
    {
        return string.Join(" ", strings);
    }

    /// <summary>
    /// calculate tax
    /// </summary>
    /// <param name="price">price, should be an integer</param>
    /// <param name="taxRate">tax rate, should be in range (0, 1)</param>
    [KernelFunction]
    [Description("calculate tax.")]
    public async Task<string> CalculateTax(int price, float taxRate)
    {
        return $"tax is {price * taxRate}";
    }

    public static async Task RunAsync(IKernelBuilder kernelBuilder, OpenAIPromptExecutionSettings settings)
    {
        var instance = new Example03_Agent_FunctionCall();
        settings.ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions;
        var kernel = kernelBuilder.Build();
        kernel.Plugins.AddFromObject(instance);
        var agent = kernel
            .ToSemanticKernelAgent(
                name: "agent",
                systemMessage: "You are a helpful AI assistant, if you get message with [TERMINATE], then terminate conversation.",
                settings: settings)
            .RegisterMessageConnector()
            .RegisterMiddleware(async (messages, options, agent2, ct) =>
            {
                var reply = await agent2.GenerateReplyAsync(messages, options, ct);
                if (reply.GetContent()?.ToLower().Contains("terminate") is true)
                {
                    return new TextMessage(Role.Assistant, GroupChatExtension.TERMINATE, from: reply.From);
                }
                return reply;
            })
            .RegisterPrintMessage();
        
        // talk to the assistant agent
        // var upperCase = await agent.SendAsync("convert to upper case: hello world");
        // upperCase.GetContent()?.Should().Be("HELLO WORLD");
        // upperCase.Should().BeOfType<AggregateMessage<ToolCallMessage, ToolCallResultMessage>>();
        // upperCase.GetToolCalls().Should().HaveCount(1);
        // upperCase.GetToolCalls().First().FunctionName.Should().Be(nameof(UpperCase));

        // var concatString = await agent.SendAsync("concatenate strings: a, b, c, d, e");
        // concatString.GetContent()?.Should().Be("a b c d e");
        // concatString.Should().BeOfType<AggregateMessage<ToolCallMessage, ToolCallResultMessage>>();
        // concatString.GetToolCalls().Should().HaveCount(1);
        // concatString.GetToolCalls().First().FunctionName.Should().Be(nameof(ConcatString));

        // var calculateTax = await agent.SendAsync("calculate tax: 100, 0.1");
        // calculateTax.GetContent().Should().Be("tax is 10");
        // calculateTax.Should().BeOfType<AggregateMessage<ToolCallMessage, ToolCallResultMessage>>();
        // calculateTax.GetToolCalls().Should().HaveCount(1);
        // calculateTax.GetToolCalls().First().FunctionName.Should().Be(nameof(CalculateTax));

        var userProxy = new UserProxyAgent(name: "user", defaultReply: GroupChatExtension.TERMINATE, humanInputMode: HumanInputMode.ALWAYS)
            .RegisterPrintMessage();

        await userProxy.InitiateChatAsync(agent);
    }
}