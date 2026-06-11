// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.IO;
using LoreVcs.Types;
using LoreVcs.Types.Enums;
using LoreVcs.Types.Args;
using LoreVcs.Types.Events;
// Since all functions are static in the class Native we can import them in one go
// and avoid the tedious typing Native.Lore_some_function()
using static LoreVcs.Interop.Native;

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

const string COMMIT_MESSAGE = "Initial commit";
string REPOSITORY_NAME = "EpicRepo" + Guid.NewGuid().ToString();
string REPOSITORY_URL = ONLINE ? $"{REMOTE_URL}/{REPOSITORY_NAME}" : REPOSITORY_NAME;
string REPOSITORY_PATH = $"./LoreRepositories/{REPOSITORY_NAME}";

using var global_args = new LoreGlobalArgs { RepositoryPath = REPOSITORY_PATH, Offline = !ONLINE };

static void EventHandler(LoreEventFFI loreEvent, ulong userContext)
{
    if (loreEvent.Tag == LoreEventTag.LOG)
    {
        var logEvent = loreEvent.GetData<LoreLogEventDataFFI>();
        if (logEvent.Level > LoreLogLevel.DEBUG)
        {
            Console.WriteLine($"{logEvent.Message}");
        }
    }
}

static void create_files(string repository_name)
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

static void verify_result(string operation_name, int result)
{
    if (result != 0)
    {
        Console.WriteLine($"Lore {operation_name} failed.");
        Environment.Exit(1);
    }

    Console.WriteLine($"Lore {operation_name} success.");
}

var callback = new LoreEventCallbackConfig { Func = EventHandler };
LoreLogConfig log_config = new LoreLogConfig { FilePath = "./LoreRepositories", File = true };
int result = LoreLogConfigure(log_config);
verify_result("Setup", result);

// Create repository
// repo_args is an unmanaged type. Within a using `scope` it will be auto-disposed.
var repo_args = new LoreRepositoryCreateArgs { RepositoryUrl = REPOSITORY_URL };
using (repo_args)
{
    result = LoreRepositoryCreate(global_args, repo_args, callback);
}
verify_result("Repo Create", result);

// Create files to commit to the new repository
create_files(REPOSITORY_NAME);

// Stage file
var paths = new string[] {
    $"./LoreRepositories/{REPOSITORY_NAME}/file.txt",
    $"./LoreRepositories/{REPOSITORY_NAME}/log.txt"
};
using var stage_args = new LoreFileStageArgs { Paths = paths };
result = LoreFileStage(global_args, stage_args, callback);
verify_result("File Stage", result);

// Revision commit
using var commit_args = new LoreRevisionCommitArgs { Message = COMMIT_MESSAGE };
result = LoreRevisionCommit(global_args, commit_args, callback);
verify_result("Revision Commit", result);

if (ONLINE)
{
    // Branch push
    using var push_args = new LoreBranchPushArgs();
    result = LoreBranchPush(global_args, push_args, callback);
    verify_result("Branch Push", result);

    // Clone repository
    string REPOSITORY_PATH_CLONE = REPOSITORY_PATH + "_clone";
    using var global_args_clone = new LoreGlobalArgs { RepositoryPath = REPOSITORY_PATH_CLONE };
    using var clone_args = new LoreRepositoryCloneArgs { RepositoryUrl = REPOSITORY_URL };
    result = LoreRepositoryClone(global_args_clone, clone_args, callback);
    verify_result("Repository Clone", result);
}

result = LoreShutdown();
verify_result("Shutdown", result);
