// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.IO;
using LoreVcs.Types;
using LoreVcs.Types.Args;
using LoreVcs;
using LoreVcs.Types.Events;
using LoreVcs.Types.Enums;

// If a remote URL is provided as the first CLI arg, run in online mode (push
// the revision and clone the repository back). Otherwise run a fully offline
// example that only creates a local repository and commits a file.
// Authentication is not handled by this example; if the remote requires it,
// run `lore auth` before invoking this program.
string? REMOTE_URL = args.Length > 0 ? args[0] : null;
bool ONLINE = REMOTE_URL != null;

if (ONLINE)
{
    Console.WriteLine($"Running in online mode against: {REMOTE_URL}");
}
else
{
    Console.WriteLine("Running in offline mode (pass a remote URL as the first arg to enable push/clone)");
}

string REPOSITORY_NAME = "EpicRepo" + Guid.NewGuid().ToString();
string REPOSITORY_PATH = $"./LoreRepositories/{REPOSITORY_NAME}";
string REPOSITORY_URL = ONLINE ? $"{REMOTE_URL}/{REPOSITORY_NAME}" : REPOSITORY_NAME;

static void EventHandler(LoreEventFFI loreEvent, ulong userContext)
{
    if (loreEvent.Tag == LoreEventTag.REPOSITORY_CREATE)
    {
        var createEvent = loreEvent.GetData<LoreRepositoryCreateEventDataFFI>();
        Console.WriteLine($"{createEvent.Name}");
    }
}

static void verifyResult(string operation_name, int result)
{
    if (result != 0)
    {
        Console.WriteLine($"Lore {operation_name} failed.");
        Environment.Exit(1);
    }

    Console.WriteLine($"Lore {operation_name} success.");
}

static void createFiles(string repository_name)
{
    string[] files = {
        $"./LoreRepositories/{repository_name}/file.txt",
        $"./LoreRepositories/{repository_name}/log.txt",
    };
    foreach (string file in files)
    {
        File.WriteAllText(file, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et");
    }
}

var logConfig = new LoreLogConfig { FilePath = "./LoreRepositories", File = true };
Lore.LogConfigure(logConfig);

// Register a global logger. Disposed automatically when out of scope, or when globalLogger.Dispose() is called manually
using var globalLogger = Lore.GlobalCallback(
    LoreEventTag.LOG,
    (loreEvent, userContext) =>
    {
        var logEvent = loreEvent.GetData<LoreLogEventDataFFI>();
        if (logEvent.Level > LoreLogLevel.DEBUG)
        {
            Console.WriteLine($"{logEvent.Message}");
        }
    }
);

using var globalArgs = new LoreGlobalArgs { RepositoryPath = REPOSITORY_PATH, Offline = !ONLINE };

using var repoArgs = new LoreRepositoryCreateArgs { RepositoryUrl = REPOSITORY_URL };
var result = Lore.RepositoryCreate(globalArgs, repoArgs).Callback(EventHandler).Wait();
verifyResult("RepositoryCreate", result);

createFiles(REPOSITORY_NAME);

using var stageArgs = new LoreFileStageArgs
{
    Paths = new string[] { $"./LoreRepositories/{REPOSITORY_NAME}/file.txt", $"./LoreRepositories/{REPOSITORY_NAME}/log.txt" }
};
result = Lore.FileStage(globalArgs, stageArgs).Callback(EventHandler).Wait();
verifyResult("FileStage", result);

using var commitArgs = new LoreRevisionCommitArgs { Message = "Initial Commit" };
result = Lore.RevisionCommit(globalArgs, commitArgs).Callback(EventHandler).Wait();
verifyResult("RevisionCommit", result);

if (ONLINE)
{
    using var pushArgs = new LoreBranchPushArgs();
    result = Lore.BranchPush(globalArgs, pushArgs).Callback(EventHandler).Wait();
    verifyResult("BranchPush", result);

    using var globalArgsClone = new LoreGlobalArgs { RepositoryPath = REPOSITORY_PATH + "_clone" };
    using var cloneArgs = new LoreRepositoryCloneArgs { RepositoryUrl = REPOSITORY_URL };
    result = Lore.RepositoryClone(globalArgsClone, cloneArgs).Callback(EventHandler).Wait();
    verifyResult("RepositoryClone", result);
}

Lore.Shutdown();
