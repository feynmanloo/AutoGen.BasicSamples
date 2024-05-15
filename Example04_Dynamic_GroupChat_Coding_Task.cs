﻿using AutoGen.Core;
using AutoGen.DotnetInteractive;
using AutoGen.SemanticKernel.Extension;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AutoGen.BasicSamples;

public partial class Example04_Dynamic_GroupChat_Coding_Task
{
    public static async Task RunAsync(IKernelBuilder kernelBuilder, OpenAIPromptExecutionSettings settings)
    {
        var instance = new Example04_Dynamic_GroupChat_Coding_Task();

        // setup dotnet interactive
        var workDir = Path.Combine(Path.GetTempPath(), "InteractiveService");
        if (!Directory.Exists(workDir))
            Directory.CreateDirectory(workDir);

        using var service = new InteractiveService(workDir);
        var dotnetInteractiveFunctions = new DotnetInteractiveFunction(service);

        var result = Path.Combine(workDir, "result.txt");
        if (File.Exists(result))
            File.Delete(result);

        await service.StartAsync(workDir, default);

        var helperAgent = kernelBuilder.Build()
                .ToSemanticKernelAgent(
                    name: "helper",
                    systemMessage: "You are a helpful AI assistant.",
                    settings: settings)
            ;
        var groupAdmin = kernelBuilder.Build()
                .ToSemanticKernelAgent(
                    name: "groupAdmin",
                    systemMessage: "You are the admin of this group chat.",
                    settings: settings)
            ;
        var userProxy = new UserProxyAgent(name: "user", defaultReply: GroupChatExtension.TERMINATE, humanInputMode: HumanInputMode.NEVER)
            .RegisterPrintMessage();
        var admin = kernelBuilder.Build()
                .ToSemanticKernelAgent(
                    name: "admin",
                    systemMessage: """
                                   You are a manager who takes coding problem from user and resolve problem by splitting them into small tasks and assign each task to the most appropriate agent.
                                   Here's available agents who you can assign task to:
                                   - coder: write dotnet code to resolve task
                                   - runner: run dotnet code from coder

                                   The workflow is as follows:
                                   - You take the coding problem from user
                                   - You break the problem into small tasks. For each tasks you first ask coder to write code to resolve the task. Once the code is written, you ask runner to run the code.
                                   - Once a small task is resolved, you summarize the completed steps and create the next step.
                                   - You repeat the above steps until the coding problem is resolved.

                                   You can use the following json format to assign task to agents:
                                   ```task
                                   {
                                       "to": "{agent_name}",
                                       "task": "{a short description of the task}",
                                       "context": "{previous context from scratchpad}"
                                   }
                                   ```

                                   If you need to ask user for extra information, you can use the following format:
                                   ```ask
                                   {
                                       "question": "{question}"
                                   }
                                   ```

                                   Once the coding problem is resolved, summarize each steps and results and send the summary to the user using the following format:
                                   ```summary
                                   {
                                       "problem": "{coding problem}",
                                       "steps": [
                                           {
                                               "step": "{step}",
                                               "result": "{result}"
                                           }
                                       ]
                                   }
                                   ```

                                   Your reply must contain one of [task|ask|summary] to indicate the type of your message.
                                   """,
                    settings: settings)
                .RegisterMessageConnector()
                .RegisterPrintMessage()
            ;
        var coderAgent = kernelBuilder.Build()
                .ToSemanticKernelAgent(
                    name: "coder",
                    systemMessage:
                    @"You act as dotnet coder, you write dotnet code to resolve task. Once you finish writing code, ask runner to run the code for you.

Here're some rules to follow on writing dotnet code:
- put code between ```csharp and ```
- When creating http client, use `var httpClient = new HttpClient()`. Don't use `using var httpClient = new HttpClient()` because it will cause error when running the code.
- Try to use `var` instead of explicit type.
- Try avoid using external library, use .NET Core library instead.
- Use top level statement to write code.
- Always print out the result to console. Don't write code that doesn't print out anything.

If you need to install nuget packages, put nuget packages in the following format:
```nuget
nuget_package_name
```

If your code is incorrect, Fix the error and send the code again.

Here's some externel information
- The link to mlnet repo is: https://github.com/dotnet/machinelearning. you don't need a token to use github pr api. Make sure to include a User-Agent header, otherwise github will reject it.
",
                    settings: settings)
                .RegisterMessageConnector()
                .RegisterPrintMessage()
            ;
        var codeReviewAgent  = kernelBuilder.Build()
                .ToSemanticKernelAgent(
                    name: "reviewer",
                    systemMessage: """
                                   You are a code reviewer who reviews code from coder. You need to check if the code satisfy the following conditions:
                                   - The reply from coder contains at least one code block, e.g ```csharp and ```
                                   - There's only one code block and it's csharp code block
                                   - The code block is not inside a main function. a.k.a top level statement
                                   - The code block is not using declaration when creating http client

                                   You don't check the code style, only check if the code satisfy the above conditions.

                                   Put your comment between ```review and ```, if the code satisfies all conditions, put APPROVED in review.result field. Otherwise, put REJECTED along with comments. make sure your comment is clear and easy to understand.

                                   ## Example 1 ##
                                   ```review
                                   comment: The code satisfies all conditions.
                                   result: APPROVED
                                   ```

                                   ## Example 2 ##
                                   ```review
                                   comment: The code is inside main function. Please rewrite the code in top level statement.
                                   result: REJECTED
                                   ```

                                   """,
                    settings: settings)
                .RegisterMessageConnector()
                .RegisterPrintMessage()
            ;
        var runner = kernelBuilder.Build()
            .ToSemanticKernelAgent(
                name: "runner",
                systemMessage: "You act as dotnet runner, you run dotnet code from coder.",
                settings: settings)
            .RegisterMessageConnector()
            .RegisterDotnetCodeBlockExectionHook(interactiveService: service)
            .RegisterMiddleware(async (msgs, option, agent, ct) =>
            {
                var mostRecentCoderMessage = msgs.LastOrDefault(x => x.From == "coder") ?? throw new Exception("No coder message found");
                return await agent.GenerateReplyAsync(new[] { mostRecentCoderMessage }, option, ct);
            })
            .RegisterPrintMessage();
        var adminToCoderTransition = Transition.Create(admin, coderAgent, async (from, to, messages) =>
        {
            // the last message should be from admin
            var lastMessage = messages.Last();
            if (lastMessage.From != admin.Name)
            {
                return false;
            }

            return true;
        });
        var coderToReviewerTransition = Transition.Create(coderAgent, codeReviewAgent);
        var adminToRunnerTransition = Transition.Create(admin, runner, async (from, to, messages) =>
        {
            // the last message should be from admin
            var lastMessage = messages.Last();
            if (lastMessage.From != admin.Name)
            {
                return false;
            }

            // the previous messages should contain a message from coder
            var coderMessage = messages.FirstOrDefault(x => x.From == coderAgent.Name);
            if (coderMessage is null)
            {
                return false;
            }

            return true;
        });

        var runnerToAdminTransition = Transition.Create(runner, admin);

        var reviewerToAdminTransition = Transition.Create(codeReviewAgent, admin);

        var adminToUserTransition = Transition.Create(admin, userProxy, async (from, to, messages) =>
        {
            // the last message should be from admin
            var lastMessage = messages.Last();
            if (lastMessage.From != admin.Name)
            {
                return false;
            }

            return true;
        });

        var userToAdminTransition = Transition.Create(userProxy, admin);

        var workflow = new Graph(
            [
                adminToCoderTransition,
                coderToReviewerTransition,
                reviewerToAdminTransition,
                adminToRunnerTransition,
                runnerToAdminTransition,
                adminToUserTransition,
                userToAdminTransition,
            ]);

        // create group chat
        var groupChat = new GroupChat(
            admin: groupAdmin,
            members: [admin, coderAgent, runner, codeReviewAgent, userProxy],
            workflow: workflow);

        // task 1: retrieve the most recent pr from mlnet and save it in result.txt
        var groupChatManager = new GroupChatManager(groupChat);
        await userProxy.SendAsync(groupChatManager, "Retrieve the most recent pr from mlnet and save it in result.txt", maxRound: 30);
        
        var conversationHistory = await userProxy.InitiateChatAsync(groupChatManager, "What's the 39th of fibonacci number? Save the result in result.txt", maxRound: 10);
        
    }
}