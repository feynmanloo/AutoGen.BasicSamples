using AutoGen.Core;
using AutoGen.SemanticKernel.Extension;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AutoGen.BasicSamples;

public static class Example02_TwoAgent_MathChat
{
    public static async Task RunAsync(IKernelBuilder kernelBuilder, OpenAIPromptExecutionSettings settings)
    {
        #region code_snippet_1
        // create teacher agent
        // teacher agent will create math questions
        var teacher = kernelBuilder.Build()
            .ToSemanticKernelAgent(
                name: "teacher",
                systemMessage: @"You are a teacher that create pre-school math question for student and check answer.
        If the answer is correct, you terminate conversation by saying [TERMINATE].
        If the answer is wrong, you ask student to fix it.",
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
        // create student agent
        // student agent will answer the math questions
        var student = kernelBuilder.Build()
            .ToSemanticKernelAgent(
                name: "student",
                systemMessage: "You are a student that answer pre-school math question from teacher.",
                settings: settings)
            .RegisterMessageConnector()
            .RegisterPrintMessage();
        
        // start the conversation
        var conversation = await student.InitiateChatAsync(
            receiver: teacher,
            message: "Hey teacher, please create math question for me.",
            maxRound: 10);

        // output
        // Message from teacher
        // --------------------
        // content: Of course!Here's a math question for you:
        // 
        // What is 2 + 3 ?
        // --------------------
        // 
        // Message from student
        // --------------------
        // content: The sum of 2 and 3 is 5.
        // --------------------
        // 
        // Message from teacher
        // --------------------
        // content: [GROUPCHAT_TERMINATE]
        // --------------------
        #endregion code_snippet_1
        conversation.Count().Should().BeLessThan(10);
        conversation.Last().IsGroupChatTerminateMessage().Should().BeTrue();

    }
}