using System.Runtime.CompilerServices;
using Xunit;
using LoreVcs.Types;
using LoreVcs.Types.Args;
using LoreVcs.Types.Enums;
using LoreVcs.Types.Events;
using static LoreVcs.Interop.Native;
using System.Threading.Tasks;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace LoreVcs.Tests;

static class TestSetup
{
    [ModuleInitializer]
    internal static void Init()
    {
        var logConfig = new LoreLogConfig { Level = LoreLogLevel.DEBUG };
        LoreLogConfigure(logConfig);
    }
}

public class LoreBaseTest : IDisposable
{
    protected readonly string tempDir = string.Empty;
    protected readonly LoreGlobalArgs globalArgs = new();
    protected readonly LoreEventCallbackConfig NoOpCallback = new();
    protected readonly int maxRetries = 5;

    public LoreBaseTest()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        globalArgs = new LoreGlobalArgs
        {
            Offline = true,
            RepositoryPath = tempDir,
        };

        NoOpCallback.Func = (LoreEventFFI loreEvent, ulong userContext) => { };

        CreateRepository();
    }

    public void Dispose()
    {
        // Tear down lorelib first so it drops cached repo handles and stops
        // the sled flusher thread holding `level.pending`. Without this,
        // Directory.Delete fails on Windows with UnauthorizedAccessException.
        // Mirrors the Python SDK's teardown_method (tests/test_functions.py).
        LoreShutdown();

        for (int i = 0, delay = 1; i < maxRetries; ++i)
        {
            if (!Directory.Exists(tempDir))
            {
                return;
            }

            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(delay);
                delay *= 2;
            }
        }
    }

    protected int CreateRepository()
    {
        var args = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };
        return LoreRepositoryCreate(globalArgs, args, NoOpCallback);
    }

    protected string CreateRandomFile()
    {
        var tempFile = $"{tempDir}/" + Guid.NewGuid().ToString() + ".txt";
        File.WriteAllText(tempFile, "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et");
        return tempFile;
    }

    protected int FileStage(string tempFile, LoreEventCallbackConfig callback)
    {
        var args = new LoreFileStageArgs();
        args.Paths = new string[] { tempFile };
        return LoreFileStage(globalArgs, args, callback);
    }

    protected int RevisionCommit(LoreEventCallbackConfig callback)
    {
        var args = new LoreRevisionCommitArgs();
        args.Message = "Initial commit";
        return LoreRevisionCommit(globalArgs, args, callback);
    }
}

public class LoreVersionCommandTest
{
    [Fact]
    public void Version_Works()
    {
        var version = LoreVersion();
        Assert.NotEmpty(version);
    }
}

public class LoreRepositoryCommandTest
{
    private string tempDir = string.Empty;
    private static List<LoreEventFFI> eventsFFI = new();
    private static List<LoreLogEventDataFFI> logEventsFFI = new();
    private static List<LoreLogEventData> logEvents = new();
    private LoreGlobalArgs globalArgs = new();
    private static LoreEventCallbackConfig NoOpCallback = new();

    public LoreRepositoryCommandTest()
    {
        tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        globalArgs.Offline = true;
        globalArgs.RepositoryPath = tempDir;

        NoOpCallback.Func = (LoreEventFFI loreEvent, ulong userContext) => { };
    }

    static void NoOpHandler(LoreEventFFI loreEvent, ulong userContext)
    { }

    [Fact]
    public void Can_Create_Repository()
    {
        var args = new LoreRepositoryCreateArgs();
        args.RepositoryUrl = Guid.NewGuid().ToString();
        var result = LoreRepositoryCreate(globalArgs, args, NoOpCallback);
        Assert.Equal(0, result);
    }

    static void StoreloreEventFFICallbackHandler(LoreEventFFI loreEvent, ulong userContext)
    {
        eventsFFI.Add(loreEvent);
    }

    [Fact]
    public void Use_loreEventFFI_After_Callback_Throws()
    {
        var args = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };
        var callback = new LoreEventCallbackConfig { Func = StoreloreEventFFICallbackHandler };
        var result = LoreRepositoryCreate(globalArgs, args, callback);
        Assert.Equal(0, result);

        Assert.Throws<ObjectDisposedException>(() =>
        {
            foreach (LoreEventFFI ev in eventsFFI)
            {
                if (ev.Tag == LoreEventTag.LOG)
                {
                    var logEvent = ev.GetData<LoreLogEventDataFFI>();
                    Console.WriteLine($"Message: {logEvent.Message}");
                }
            }
        });
    }

    static void StoreLoreLogEventFFICallbackHandler(LoreEventFFI loreEvent, ulong userContext)
    {
        if (loreEvent.Tag == LoreEventTag.LOG)
        {
            using var logEvent = loreEvent.GetData<LoreLogEventDataFFI>();
            logEventsFFI.Add(logEvent);
        }
    }

    [Fact]
    public void Use_LoreLogEventFFI_After_Callback_Throws()
    {
        var args = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };
        var callback = new LoreEventCallbackConfig { Func = StoreLoreLogEventFFICallbackHandler };
        var result = LoreRepositoryCreate(globalArgs, args, callback);
        Assert.Equal(0, result);

        Assert.Throws<ObjectDisposedException>(() =>
        {
            foreach (LoreLogEventDataFFI ev in logEventsFFI)
            {
                Console.WriteLine($"Message: {ev.Message}");
            }
        });
    }

    static void StoreLoreLogEventCallbackHandler(LoreEventFFI loreEvent, ulong userContext)
    {
        if (loreEvent.Tag == LoreEventTag.LOG)
        {
            using var logEvent = loreEvent.GetData<LoreLogEventDataFFI>();
            logEvents.Add(logEvent.Clone());
        }
    }

    [Fact]
    public void Use_LoreLogEvent_After_Callback_Works()
    {
        var args = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };
        var callback = new LoreEventCallbackConfig { Func = StoreLoreLogEventCallbackHandler };
        var result = LoreRepositoryCreate(globalArgs, args, callback);
        Assert.Equal(0, result);
        Assert.Contains("Executing command: lore::repository::create", logEvents.First().Message);
        Assert.Contains("Finished command: lore::repository::create", logEvents.Last().Message);
    }

    [Fact]
    public async Task Create_Repository_Async_Works()
    {
        using var args = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };

        // First time repo creation should succeed -> result = 0
        var result = await LoreRepositoryCreateAsync(globalArgs, args, LoreRepositoryCommandTest.NoOpCallback).Task;
        Assert.Equal(0, result);

        // Attempting creating an existing repo should fail -> result = 1
        result = await LoreRepositoryCreateAsync(globalArgs, args, LoreRepositoryCommandTest.NoOpCallback).Task;
        Assert.Equal(1, result);
    }
}

public class LoreFileCommandTest : LoreBaseTest
{
    private static string expectedTestFileName = string.Empty;

    private int FileUnstage(string tempFile, LoreEventCallbackConfig callback)
    {
        var args = new LoreFileUnstageArgs();
        args.Paths = new string[] { tempFile };
        return LoreFileUnstage(globalArgs, args, callback);
    }

    private static void FileHandler(LoreEventFFI loreEvent, ulong userContext)
    {
        switch (loreEvent.Tag)
        {
            case LoreEventTag.FILE_STAGE_END:
                var fileStageEndEvent = loreEvent.GetData<LoreFileStageEndEventDataFFI>();
                Assert.Equal(1u, fileStageEndEvent.Count.FileAddCount);
                break;
            case LoreEventTag.FILE_UNSTAGE_END:
                var fileUnstageEndEvent = loreEvent.GetData<LoreFileUnstageEndEventDataFFI>();
                Assert.Equal(1ul, fileUnstageEndEvent.Count.FileUnstagedCount);
                break;
            case LoreEventTag.FILE_INFO:
                var fileInfoEvent = loreEvent.GetData<LoreFileInfoEventDataFFI>();
                Assert.Equal(Path.GetFileName(expectedTestFileName), fileInfoEvent.Path);
                Assert.True(fileInfoEvent.IsFile);
                break;
        }
    }

    [Fact]
    public void File_Stage_Works()
    {
        var tempFile = CreateRandomFile();
        var callback = new LoreEventCallbackConfig { Func = FileHandler };
        var result = FileStage(tempFile, callback);

        Assert.True(result == 0);
    }

    [Fact]
    public void File_Unstage_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        var callback = new LoreEventCallbackConfig { Func = FileHandler };
        var result = FileUnstage(tempFile, callback);
        Assert.True(result == 0);
    }

    [Fact]
    public void File_Info_Works()
    {
        expectedTestFileName = CreateRandomFile();
        FileStage(expectedTestFileName, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var args = new LoreFileInfoArgs();
        args.Paths = new string[] { expectedTestFileName };
        var callback = new LoreEventCallbackConfig { Func = FileHandler };
        var result = LoreFileInfo(globalArgs, args, callback);
    }
}

public class LoreRevisionCommandTest : LoreBaseTest
{
    private static string expectedTestFileName = string.Empty;

    private static void RevisionHandler(LoreEventFFI loreEvent, ulong userContext)
    {
        switch (loreEvent.Tag)
        {
            case LoreEventTag.REVISION_COMMIT_END:
                var revisionCommitEvent = loreEvent.GetData<LoreRevisionCommitEndEventDataFFI>();
                Assert.Equal(1u, revisionCommitEvent.Count.FileCount);
                break;
            case LoreEventTag.REVISION_HISTORY_ENTRY:
                var revisionHistoryEvent = loreEvent.GetData<LoreRevisionHistoryEntryEventDataFFI>();
                Assert.Equal(1ul, revisionHistoryEvent.RevisionNumber);
                break;
        }
    }

    [Fact]
    public void Revision_Commit_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        var callback = new LoreEventCallbackConfig { Func = RevisionHandler };
        var result = RevisionCommit(callback);

        Assert.True(result == 0);
    }

    [Fact]
    public void Revision_History_Works()
    {
        var callback = new LoreEventCallbackConfig { Func = RevisionHandler };
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        var result = RevisionCommit(callback);
        Assert.True(result == 0);

        var args = new LoreRevisionHistoryArgs();
        result = LoreRevisionHistory(globalArgs, args, callback);

        Assert.True(result == 0);
    }
}

public class LoreRepositoryStatusCommandTest : LoreBaseTest
{
    [Fact]
    public void Repository_Status_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);

        var revisionReceived = false;
        var fileEvents = new Dictionary<string, (bool flagStaged, LoreFileAction action)>();

        var callback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                switch (loreEvent.Tag)
                {
                    case LoreEventTag.REPOSITORY_STATUS_REVISION:
                        var revEvent = loreEvent.GetData<LoreRepositoryStatusRevisionEventDataFFI>();
                        Assert.Equal("main", revEvent.BranchName);

                        Assert.IsType<LoreRepositoryId>(revEvent.Repository);
                        var repoHex = Convert.ToHexString(revEvent.Repository.Data).ToLowerInvariant();
                        Assert.NotEmpty(repoHex);
                        Assert.NotEqual(new string('0', repoHex.Length), repoHex);

                        Assert.IsType<LoreBranchId>(revEvent.Branch);
                        var branchHex = Convert.ToHexString(revEvent.Branch.Data).ToLowerInvariant();
                        Assert.NotEmpty(branchHex);
                        Assert.NotEqual(new string('0', branchHex.Length), branchHex);

                        revisionReceived = true;
                        break;
                    case LoreEventTag.REPOSITORY_STATUS_FILE:
                        var fileEvent = loreEvent.GetData<LoreRepositoryStatusFileEventDataFFI>();
                        fileEvents[fileEvent.Path] = (fileEvent.FlagStaged, fileEvent.Action);
                        break;
                }
            }
        };

        var args = new LoreRepositoryStatusArgs { Staged = true, Scan = true, SyncPoint = true };
        var result = LoreRepositoryStatus(globalArgs, args, callback);

        Assert.Equal(0, result);
        Assert.True(revisionReceived);

        var stagedFilename = Path.GetFileName(tempFile);
        Assert.True(fileEvents.ContainsKey(stagedFilename));
        Assert.True(fileEvents[stagedFilename].flagStaged);
        Assert.Equal(LoreFileAction.ADD, fileEvents[stagedFilename].action);
    }
}

public class LoreFileCommandExtendedTest : LoreBaseTest
{
    [Fact]
    public void File_History_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var historyPaths = new List<string>();

        var callback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.FILE_HISTORY)
                {
                    var historyEvent = loreEvent.GetData<LoreFileHistoryEventDataFFI>();
                    historyPaths.Add(historyEvent.Path);
                }
            }
        };

        var args = new LoreFileHistoryArgs { Path = tempFile };
        var result = LoreFileHistory(globalArgs, args, callback);

        Assert.Equal(0, result);
        Assert.Contains(Path.GetFileName(tempFile), historyPaths);
    }

    [Fact]
    public void File_Write_Works()
    {
        var tempFile = CreateRandomFile();
        var version1Content = "Version 1 content";
        File.WriteAllText(tempFile, version1Content);

        FileStage(tempFile, NoOpCallback);

        var firstRevisionHash = new List<string>();
        var commitCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.REVISION_COMMIT_REVISION)
                {
                    var revEvent = loreEvent.GetData<LoreRevisionCommitRevisionEventDataFFI>();
                    firstRevisionHash.Add(Convert.ToHexString(revEvent.Revision.Data).ToLowerInvariant());
                }
            }
        };
        RevisionCommit(commitCallback);
        Assert.Single(firstRevisionHash);

        File.WriteAllText(tempFile, "Version 2 content - updated");
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var outputPath = tempFile + ".old";
        var args = new LoreFileWriteArgs
        {
            Path = tempFile,
            Revision = firstRevisionHash[0],
            Output = outputPath
        };
        var result = LoreFileWrite(globalArgs, args, NoOpCallback);

        Assert.Equal(0, result);
        Assert.Equal(version1Content, File.ReadAllText(outputPath));
    }

    [Fact]
    public void File_Metadata_Set_And_List_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);

        var setArgs = new LoreFileMetadataSetArgs
        {
            Paths = new[] { tempFile },
            Keys = new[] { "test-key" },
            Values = new[] { "test-value" },
            Formats = new[] { LoreMetadataType.STRING },
            Entries = new uint[] { 1 }
        };
        var result = LoreFileMetadataSet(globalArgs, setArgs, NoOpCallback);
        Assert.Equal(0, result);

        var metadataEvents = new List<LoreMetadataEventData>();
        var listCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.METADATA)
                {
                    var metaEvent = loreEvent.GetData<LoreMetadataEventDataFFI>();
                    metadataEvents.Add(metaEvent.Clone());
                }
            }
        };

        var listArgs = new LoreFileMetadataListArgs { Path = tempFile };
        result = LoreFileMetadataList(globalArgs, listArgs, listCallback);

        Assert.Equal(0, result);
        Assert.Contains(metadataEvents, e => e.Key == "test-key" && e.Value.String == "test-value");
    }
}

public class LoreRevisionCommandExtendedTest : LoreBaseTest
{
    [Fact]
    public void Revision_Amend_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var amendedMessage = "amended commit message";
        var amendArgs = new LoreRevisionAmendArgs { Message = amendedMessage };
        var result = LoreRevisionAmend(globalArgs, amendArgs, NoOpCallback);
        Assert.Equal(0, result);

        var commitMessages = new List<string>();
        var historyCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.METADATA)
                {
                    var metaEvent = loreEvent.GetData<LoreMetadataEventDataFFI>();
                    if (metaEvent.Key == "message")
                        commitMessages.Add(metaEvent.Value.String);
                }
            }
        };

        var historyArgs = new LoreRevisionHistoryArgs();
        result = LoreRevisionHistory(globalArgs, historyArgs, historyCallback);
        Assert.Equal(0, result);
        Assert.Contains(amendedMessage, commitMessages);
    }

    [Fact]
    public void Revision_Diff_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var originalRevisionHash = new List<string>();
        var infoCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.REVISION_INFO)
                {
                    var infoEvent = loreEvent.GetData<LoreRevisionInfoEventDataFFI>();
                    originalRevisionHash.Add(Convert.ToHexString(infoEvent.Revision.Data).ToLowerInvariant());
                }
            }
        };

        var infoArgs = new LoreRevisionInfoArgs();
        var result = LoreRevisionInfo(globalArgs, infoArgs, infoCallback);
        Assert.Equal(0, result);
        Assert.Single(originalRevisionHash);

        var newFile = CreateRandomFile();
        FileStage(newFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var diffFiles = new List<LoreRevisionDiffFileEventData>();
        var diffCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.REVISION_DIFF_FILE)
                {
                    var diffEvent = loreEvent.GetData<LoreRevisionDiffFileEventDataFFI>();
                    diffFiles.Add(diffEvent.Clone());
                }
            }
        };

        var diffArgs = new LoreRevisionDiffArgs { RevisionSource = originalRevisionHash[0] };
        result = LoreRevisionDiff(globalArgs, diffArgs, diffCallback);
        Assert.Equal(0, result);
        Assert.Contains(diffFiles, f => f.Path == Path.GetFileName(newFile) && f.Action == LoreFileAction.ADD);
    }

    [Fact]
    public void Revision_Info_With_HashArray_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var revisionInfo = new List<LoreRevisionInfoEventData>();
        var callback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.REVISION_INFO)
                {
                    var infoEvent = loreEvent.GetData<LoreRevisionInfoEventDataFFI>();
                    revisionInfo.Add(infoEvent.Clone());
                }
            }
        };

        var args = new LoreRevisionInfoArgs();
        var result = LoreRevisionInfo(globalArgs, args, callback);

        Assert.Equal(0, result);
        Assert.Single(revisionInfo);
        Assert.Equal((ulong)1, revisionInfo[0].RevisionNumber);
        Assert.Equal(32, revisionInfo[0].Revision.Length);
        Assert.IsType<byte[][]>(revisionInfo[0].Parent);
    }

    [Fact]
    public void Revision_Metadata_Set_And_List_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);

        var setArgs = new LoreRevisionMetadataSetArgs
        {
            Keys = new[] { "custom-key" },
            Values = new[] { "custom-value" },
            Formats = new[] { LoreMetadataType.STRING }
        };
        var result = LoreRevisionMetadataSet(globalArgs, setArgs, NoOpCallback);
        Assert.Equal(0, result);

        RevisionCommit(NoOpCallback);

        var metadataEntries = new List<LoreMetadataEventData>();
        var listCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.METADATA)
                {
                    var metaEvent = loreEvent.GetData<LoreMetadataEventDataFFI>();
                    metadataEntries.Add(metaEvent.Clone());
                }
            }
        };

        var listArgs = new LoreRevisionMetadataListArgs();
        result = LoreRevisionMetadataList(globalArgs, listArgs, listCallback);

        Assert.Equal(0, result);
        Assert.Contains(metadataEntries, e => e.Key == "custom-key" && e.Value.String == "custom-value");
    }

    [Fact]
    public void Revision_Find_By_Metadata_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);

        var setArgs = new LoreRevisionMetadataSetArgs
        {
            Keys = new[] { "search-key" },
            Values = new[] { "search-value" },
            Formats = new[] { LoreMetadataType.STRING }
        };
        LoreRevisionMetadataSet(globalArgs, setArgs, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var findResults = new List<LoreRevisionFindEventData>();
        var findCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.REVISION_FIND)
                {
                    var findEvent = loreEvent.GetData<LoreRevisionFindEventDataFFI>();
                    findResults.Add(findEvent.Clone());
                }
            }
        };

        var findArgs = new LoreRevisionFindArgs { Key = "search-key", Value = "search-value" };
        var result = LoreRevisionFind(globalArgs, findArgs, findCallback);

        Assert.Equal(0, result);
        Assert.Single(findResults);
        Assert.Equal(32, findResults[0].Signature.Length);
    }

    [Fact]
    public void Revision_Find_By_Number_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var findResults = new List<LoreRevisionFindEventData>();
        var findCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.REVISION_FIND)
                {
                    var findEvent = loreEvent.GetData<LoreRevisionFindEventDataFFI>();
                    findResults.Add(findEvent.Clone());
                }
            }
        };

        var findArgs = new LoreRevisionFindArgs { Number = 1 };
        var result = LoreRevisionFind(globalArgs, findArgs, findCallback);

        Assert.Equal(0, result);
        Assert.Single(findResults);
        Assert.Equal(32, findResults[0].Signature.Length);
    }
}

public class LoreBranchCommandExtendedTest : LoreBaseTest
{
    private int BranchCreate(string branchName, LoreEventCallbackConfig callback)
    {
        var args = new LoreBranchCreateArgs { Branch = branchName };
        return LoreBranchCreate(globalArgs, args, callback);
    }

    private int BranchSwitch(string branchName, LoreEventCallbackConfig callback)
    {
        var args = new LoreBranchSwitchArgs { Branch = branchName };
        return LoreBranchSwitch(globalArgs, args, callback);
    }

    [Fact]
    public void Branch_Merge_With_Conflict_Resolution_Works()
    {
        var featureBranch = "feature-branch";

        var conflictFile = Path.Combine(tempDir, "conflict-file.txt");
        File.WriteAllText(conflictFile, "Main branch content");
        FileStage(conflictFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        BranchCreate(featureBranch, NoOpCallback);
        File.WriteAllText(conflictFile, "Feature branch content");
        FileStage(conflictFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        BranchSwitch("main", NoOpCallback);
        File.WriteAllText(conflictFile, "Main branch conflicting content");
        FileStage(conflictFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var conflictPaths = new List<string>();
        var mergeCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.BRANCH_MERGE_CONFLICT_FILE)
                {
                    var conflictEvent = loreEvent.GetData<LoreBranchMergeConflictFileEventDataFFI>();
                    conflictPaths.Add(conflictEvent.Path);
                }
            }
        };

        var mergeArgs = new LoreBranchMergeStartArgs { Branch = featureBranch, Message = "merge feature branch" };
        var result = LoreBranchMergeStart(globalArgs, mergeArgs, mergeCallback);
        Assert.Equal(0, result);
        Assert.NotEmpty(conflictPaths);

        var stageFileEvents = new List<string>();
        var resolveCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.FILE_STAGE_FILE)
                {
                    var stageEvent = loreEvent.GetData<LoreFileStageFileEventDataFFI>();
                    stageFileEvents.Add(stageEvent.Path);
                }
            }
        };

        var resolveArgs = new LoreBranchMergeResolveMineArgs { Paths = new[] { conflictFile } };
        result = LoreBranchMergeResolveMine(globalArgs, resolveArgs, resolveCallback);
        Assert.Equal(0, result);
        Assert.NotEmpty(stageFileEvents);
    }

    [Fact]
    public void Branch_Unicode_Names_Works()
    {
        var unicodeBranch = "feature/\U0001f680-rocket";

        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        BranchCreate(unicodeBranch, NoOpCallback);

        var branchNames = new List<string>();
        var listCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.BRANCH_LIST_ENTRY)
                {
                    var listEvent = loreEvent.GetData<LoreBranchListEntryEventDataFFI>();
                    branchNames.Add(listEvent.Name);
                }
            }
        };

        var listArgs = new LoreBranchListArgs();
        var result = LoreBranchList(globalArgs, listArgs, listCallback);
        Assert.Equal(0, result);
        Assert.Contains(unicodeBranch, branchNames);
    }

    [Fact]
    public void Branch_Info_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var branchInfoEvents = new List<LoreBranchInfoEventData>();
        var callback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.BRANCH_INFO)
                {
                    var infoEvent = loreEvent.GetData<LoreBranchInfoEventDataFFI>();
                    branchInfoEvents.Add(infoEvent.Clone());
                }
            }
        };

        var args = new LoreBranchInfoArgs();
        var result = LoreBranchInfo(globalArgs, args, callback);

        Assert.Equal(0, result);
        Assert.Single(branchInfoEvents);
        Assert.Equal("main", branchInfoEvents[0].Name);
    }
}

public class LoreUnicodeSupportTest : LoreBaseTest
{
    [Fact]
    public void Unicode_In_Filenames_Content_And_CommitMessages()
    {
        var unicodeFilename = "öäÄÅ的ЛЛЛ-こんにちは-\U0001f680.txt";
        var unicodeContent = "Hello 世界! Привет мир! \U0001f389 日本語, Русский, العربية";
        var unicodeCommitMessage = "add unicode file with öäÄÅ的ЛЛЛ \U0001f680";

        var unicodeFile = Path.Combine(tempDir, unicodeFilename);
        File.WriteAllText(unicodeFile, unicodeContent);

        var stagedPaths = new List<string>();
        var stageCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.FILE_STAGE_FILE)
                {
                    var stageEvent = loreEvent.GetData<LoreFileStageFileEventDataFFI>();
                    stagedPaths.Add(stageEvent.Path);
                }
            }
        };
        FileStage(unicodeFile, stageCallback);
        Assert.Contains(unicodeFilename, stagedPaths);

        var commitArgs = new LoreRevisionCommitArgs { Message = unicodeCommitMessage };
        var result = LoreRevisionCommit(globalArgs, commitArgs, NoOpCallback);
        Assert.Equal(0, result);

        var commitMessages = new List<string>();
        var historyCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.METADATA)
                {
                    var metaEvent = loreEvent.GetData<LoreMetadataEventDataFFI>();
                    if (metaEvent.Key == "message")
                        commitMessages.Add(metaEvent.Value.String);
                }
            }
        };

        var historyArgs = new LoreRevisionHistoryArgs();
        result = LoreRevisionHistory(globalArgs, historyArgs, historyCallback);
        Assert.Equal(0, result);
        Assert.Contains(unicodeCommitMessage, commitMessages);

        var outputPath = Path.Combine(tempDir, "unicode_output.txt");
        var writeArgs = new LoreFileWriteArgs { Path = unicodeFile, Output = outputPath };
        result = LoreFileWrite(globalArgs, writeArgs, NoOpCallback);
        Assert.Equal(0, result);
        Assert.Equal(unicodeContent, File.ReadAllText(outputPath));
    }
}

public class LoreRepositoryInstanceCommandTest : LoreBaseTest
{
    [Fact]
    public void Repository_Instance_List_Works()
    {
        var instanceEvents = new List<LoreRepositoryInstanceEventData>();
        var callback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong userContext) =>
            {
                if (loreEvent.Tag == LoreEventTag.REPOSITORY_INSTANCE)
                {
                    var instanceEvent = loreEvent.GetData<LoreRepositoryInstanceEventDataFFI>();
                    instanceEvents.Add(instanceEvent.Clone());
                }
            }
        };

        var args = new LoreRepositoryInstanceListArgs();
        var result = LoreRepositoryInstanceList(globalArgs, args, callback);

        Assert.Equal(0, result);
        Assert.True(instanceEvents.Count >= 1);
        Assert.NotEmpty(instanceEvents[0].Path);
    }
}

public class LoreBranchCommandTest : LoreBaseTest
{
    private static string expectedBranchName = "test-branch";
    private static string expectedTestFileName = string.Empty;

    private static void BranchHandler(LoreEventFFI loreEvent, ulong userContext)
    {
        switch (loreEvent.Tag)
        {
            case LoreEventTag.BRANCH_CREATE:
                var branchCreateEvent = loreEvent.GetData<LoreBranchCreateEventDataFFI>();
                Assert.Equal(expectedBranchName, branchCreateEvent.Name);
                break;
            case LoreEventTag.BRANCH_SWITCH_END:
                var branchSwitchEndEvent = loreEvent.GetData<LoreBranchSwitchEndEventDataFFI>();
                Assert.Equal("main", branchSwitchEndEvent.Branch.Name);
                break;
            case LoreEventTag.BRANCH_LIST_ENTRY:
                var branchListEntryEvent = loreEvent.GetData<LoreBranchListEntryEventDataFFI>();
                if (branchListEntryEvent.Name == "main")
                {
                    Assert.Empty(branchListEntryEvent.Stack);
                }
                else
                {
                    Assert.Single(branchListEntryEvent.Stack);
                    Assert.Equal(16, branchListEntryEvent.Stack[0].Branch.Data.Length);
                    Assert.Equal(32, branchListEntryEvent.Stack[0].Revision.Data.Length);
                }
                break;
            case LoreEventTag.BRANCH_LIST_END:
                var branchListEndEvent = loreEvent.GetData<LoreBranchListEndEventDataFFI>();
                Assert.Equal(2ul, branchListEndEvent.Count);
                break;
            case LoreEventTag.BRANCH_DIFF_CHANGE:
                var branchDiffChangeEvent = loreEvent.GetData<LoreBranchDiffChangeEventDataFFI>();
                var filename = Path.GetFileName(expectedTestFileName);
                Assert.Equal(filename, branchDiffChangeEvent.Change.Path);
                Assert.Equal(LoreFileAction.ADD, branchDiffChangeEvent.Change.Action);
                break;
            case LoreEventTag.BRANCH_ARCHIVE:
                var branchArchiveEvent = loreEvent.GetData<LoreBranchArchiveEventDataFFI>();
                Assert.Equal(expectedBranchName, branchArchiveEvent.Name);
                break;
        }
    }

    private int BranchCreate(LoreEventCallbackConfig callback)
    {
        var argsCreate = new LoreBranchCreateArgs { Branch = expectedBranchName };
        return LoreBranchCreate(globalArgs, argsCreate, callback);
    }

    private int BranchSwitch(LoreEventCallbackConfig callback)
    {
        var argsSwitch = new LoreBranchSwitchArgs();
        argsSwitch.Branch = "main";
        return LoreBranchSwitch(globalArgs, argsSwitch, callback);
    }

    [Fact]
    public void Branch_Create_Works()
    {
        expectedTestFileName = CreateRandomFile();
        FileStage(expectedTestFileName, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var callback = new LoreEventCallbackConfig { Func = BranchHandler };
        var result = BranchCreate(callback);
        Assert.True(result == 0);
    }

    [Fact]
    public void Branch_Diff_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        BranchCreate(NoOpCallback);
        expectedTestFileName = CreateRandomFile();
        FileStage(expectedTestFileName, NoOpCallback);
        RevisionCommit(NoOpCallback);

        var argsDiff = new LoreBranchDiffArgs();
        argsDiff.Target = "main";
        var callback = new LoreEventCallbackConfig { Func = BranchHandler };
        var result = LoreBranchDiff(globalArgs, argsDiff, callback);

        Assert.True(result == 0);
    }

    [Fact]
    public void Branch_Switch_Works()
    {
        // Create a file and revision on "main"
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);
        // Create a new branch and add a file+revision on "branch"
        BranchCreate(NoOpCallback);
        expectedTestFileName = CreateRandomFile();
        FileStage(expectedTestFileName, NoOpCallback);
        RevisionCommit(NoOpCallback);
        var callback = new LoreEventCallbackConfig { Func = BranchHandler };
        var result = BranchSwitch(callback);
        Assert.True(result == 0);
        Assert.True(!File.Exists(expectedTestFileName));
    }

    [Fact]
    public void Branch_List_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);
        BranchCreate(NoOpCallback);

        var argsList = new LoreBranchListArgs();
        var callback = new LoreEventCallbackConfig { Func = BranchHandler };
        var result = LoreBranchList(globalArgs, argsList, callback);

        Assert.True(result == 0);
    }

    [Fact]
    public void Branch_Archive_Works()
    {
        var tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        BranchCreate(NoOpCallback);
        tempFile = CreateRandomFile();
        FileStage(tempFile, NoOpCallback);
        RevisionCommit(NoOpCallback);

        BranchSwitch(NoOpCallback);

        var argsArchive = new LoreBranchArchiveArgs();
        argsArchive.Branch = expectedBranchName;
        var callback = new LoreEventCallbackConfig { Func = BranchHandler };
        var result = LoreBranchArchive(globalArgs, argsArchive, callback);

        Assert.True(result == 0);
    }
}

public class LoreStorageCommandTest : LoreBaseTest
{
    private static readonly byte[] TestPartition = Enumerable.Repeat((byte)0x11, 16).ToArray();
    private static readonly byte[] TestContext = Enumerable.Repeat((byte)0x22, 16).ToArray();

    private ulong OpenInMemoryStore()
    {
        ulong handleId = 0;
        var callback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong _) =>
            {
                if (loreEvent.Tag == LoreEventTag.STORAGE_OPENED)
                {
                    handleId = loreEvent.GetData<LoreStorageOpenedEventDataFFI>().HandleId;
                }
            }
        };

        using var openArgs = new LoreStorageOpenArgs { RepositoryPath = string.Empty, InMemory = true };
        var result = LoreStorageOpen(globalArgs, openArgs, callback);

        Assert.Equal(0, result);
        Assert.NotEqual((ulong)0, handleId);
        return handleId;
    }

    private int CloseStore(ulong handleId)
    {
        using var closeArgs = new LoreStorageCloseArgs { Handle = new LoreStore(handleId) };
        return LoreStorageClose(globalArgs, closeArgs, NoOpCallback);
    }

    [Fact]
    public void Storage_Open_Close_Works()
    {
        var handleId = OpenInMemoryStore();
        var closeResult = CloseStore(handleId);
        Assert.Equal(0, closeResult);
    }

    [Fact]
    public void Storage_Put_Get_Works()
    {
        var handleId = OpenInMemoryStore();
        try
        {
            var payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();

            var putAddresses = new List<LoreAddress>();
            var putCallback = new LoreEventCallbackConfig
            {
                Func = (LoreEventFFI loreEvent, ulong _) =>
                {
                    if (loreEvent.Tag == LoreEventTag.STORAGE_PUT_ITEM_COMPLETE)
                    {
                        var ev = loreEvent.GetData<LoreStoragePutItemCompleteEventDataFFI>().Clone();
                        Assert.Equal(LoreErrorCode.NONE, ev.ErrorCode);
                        putAddresses.Add(ev.Address);
                    }
                }
            };

            using var putArgs = new LoreStoragePutArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStoragePutItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(TestPartition),
                        Context = new LoreContext(TestContext),
                        Data = payload,
                        RemoteWrite = false,
                    }
                }
            };
            var putResult = LoreStoragePut(globalArgs, putArgs, putCallback);
            Assert.Equal(0, putResult);
            Assert.Single(putAddresses);

            var receivedBytes = new List<byte>();
            var getCompletes = 0;
            var getCallback = new LoreEventCallbackConfig
            {
                Func = (LoreEventFFI loreEvent, ulong _) =>
                {
                    if (loreEvent.Tag == LoreEventTag.STORAGE_GET_DATA)
                    {
                        var ev = loreEvent.GetData<LoreStorageGetDataEventDataFFI>().Clone();
                        receivedBytes.AddRange(ev.Bytes);
                    }
                    else if (loreEvent.Tag == LoreEventTag.STORAGE_GET_ITEM_COMPLETE)
                    {
                        var ev = loreEvent.GetData<LoreStorageGetItemCompleteEventDataFFI>().Clone();
                        Assert.Equal(LoreErrorCode.NONE, ev.ErrorCode);
                        getCompletes++;
                    }
                }
            };

            using var getArgs = new LoreStorageGetArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStorageGetItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(TestPartition),
                        Address = putAddresses[0],
                        Streaming = false,
                    }
                }
            };
            var getResult = LoreStorageGet(globalArgs, getArgs, getCallback);
            Assert.Equal(0, getResult);
            Assert.Equal(1, getCompletes);
            Assert.Equal(payload, receivedBytes.ToArray());
        }
        finally
        {
            CloseStore(handleId);
        }
    }

    [Fact]
    public async Task Storage_Open_Close_Async_Works()
    {
        ulong handleId = 0;
        var openCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong _) =>
            {
                if (loreEvent.Tag == LoreEventTag.STORAGE_OPENED)
                {
                    handleId = loreEvent.GetData<LoreStorageOpenedEventDataFFI>().HandleId;
                }
            }
        };

        using var openArgs = new LoreStorageOpenArgs { RepositoryPath = string.Empty, InMemory = true };
        var openResult = await LoreStorageOpenAsync(globalArgs, openArgs, openCallback).Task;
        Assert.Equal(0, openResult);
        Assert.NotEqual((ulong)0, handleId);

        using var closeArgs = new LoreStorageCloseArgs { Handle = new LoreStore(handleId) };
        var closeResult = await LoreStorageCloseAsync(globalArgs, closeArgs, NoOpCallback).Task;
        Assert.Equal(0, closeResult);
    }

    [Fact]
    public async Task Storage_Put_Get_Async_Works()
    {
        ulong handleId = 0;
        var openCallback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong _) =>
            {
                if (loreEvent.Tag == LoreEventTag.STORAGE_OPENED)
                {
                    handleId = loreEvent.GetData<LoreStorageOpenedEventDataFFI>().HandleId;
                }
            }
        };
        using var openArgs = new LoreStorageOpenArgs { RepositoryPath = string.Empty, InMemory = true };
        await LoreStorageOpenAsync(globalArgs, openArgs, openCallback).Task;
        Assert.NotEqual((ulong)0, handleId);

        try
        {
            var payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();

            var putAddresses = new List<LoreAddress>();
            var putCallback = new LoreEventCallbackConfig
            {
                Func = (LoreEventFFI loreEvent, ulong _) =>
                {
                    if (loreEvent.Tag == LoreEventTag.STORAGE_PUT_ITEM_COMPLETE)
                    {
                        var ev = loreEvent.GetData<LoreStoragePutItemCompleteEventDataFFI>().Clone();
                        Assert.Equal(LoreErrorCode.NONE, ev.ErrorCode);
                        putAddresses.Add(ev.Address);
                    }
                }
            };

            using var putArgs = new LoreStoragePutArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStoragePutItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(TestPartition),
                        Context = new LoreContext(TestContext),
                        Data = payload,
                        RemoteWrite = false,
                    }
                }
            };
            var putResult = await LoreStoragePutAsync(globalArgs, putArgs, putCallback).Task;
            Assert.Equal(0, putResult);
            Assert.Single(putAddresses);

            var receivedBytes = new List<byte>();
            var getCompletes = 0;
            var getCallback = new LoreEventCallbackConfig
            {
                Func = (LoreEventFFI loreEvent, ulong _) =>
                {
                    if (loreEvent.Tag == LoreEventTag.STORAGE_GET_DATA)
                    {
                        var ev = loreEvent.GetData<LoreStorageGetDataEventDataFFI>().Clone();
                        receivedBytes.AddRange(ev.Bytes);
                    }
                    else if (loreEvent.Tag == LoreEventTag.STORAGE_GET_ITEM_COMPLETE)
                    {
                        var ev = loreEvent.GetData<LoreStorageGetItemCompleteEventDataFFI>().Clone();
                        Assert.Equal(LoreErrorCode.NONE, ev.ErrorCode);
                        getCompletes++;
                    }
                }
            };

            using var getArgs = new LoreStorageGetArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStorageGetItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(TestPartition),
                        Address = putAddresses[0],
                        Streaming = false,
                    }
                }
            };
            var getResult = await LoreStorageGetAsync(globalArgs, getArgs, getCallback).Task;
            Assert.Equal(0, getResult);
            Assert.Equal(1, getCompletes);
            Assert.Equal(payload, receivedBytes.ToArray());
        }
        finally
        {
            using var closeArgs = new LoreStorageCloseArgs { Handle = new LoreStore(handleId) };
            await LoreStorageCloseAsync(globalArgs, closeArgs, NoOpCallback).Task;
        }
    }
}