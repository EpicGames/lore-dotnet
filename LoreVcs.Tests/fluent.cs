using Xunit;
using System.Linq;
using System.Threading.Tasks;
using LoreVcs.Types;
using LoreVcs.Types.Args;
using LoreVcs.Types.Events;
using LoreVcs.Types.Enums;

namespace LoreVcs.Tests;

public class LoreFluentAPITests
{
    private string repositoryUrl = string.Empty;
    public LoreFluentAPITests()
    {
        repositoryUrl = Guid.NewGuid().ToString();
    }

    [Fact]
    public void Wait_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var result = Lore.RepositoryCreate(globalArgs, repositoryArgs).Wait();

        Assert.True(result == 0);
    }

    [Fact]
    public async Task WaitAsync_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var task = Lore.RepositoryCreate(globalArgs, repositoryArgs).WaitAsync();
        Assert.NotNull(task);

        var result = await task;
        Assert.True(result == 0);
    }

    [Fact]
    public async Task Same_User_Context_Multiple_WaitAsync_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        List<Task<int>> tasks = new();
        const int numTasks = 5;
        for (int i = 0; i < numTasks; ++i)
        {
            var guid = Guid.NewGuid().ToString();
            var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir + guid };
            var repo = guid;
            var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repo };
            var task = Lore.RepositoryCreate(globalArgs, repositoryArgs)
                            .Callback((LoreEventFFI loreEvent, ulong userContext) =>
                            {
                                Assert.Equal(123ul, userContext);
                            })
                            .UserContext(123)
                            .WaitAsync();
            tasks.Add(task);
        }

        for (int i = 0; i < numTasks; ++i)
        {
            var task = tasks[i];
            Assert.NotNull(task);
            var result = await task;
            Assert.Equal(0, result);
        }
    }

    [Fact]
    public async Task AsyncIter_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = Lore.RepositoryCreate(globalArgs, repositoryArgs).AsyncIter();
        var completeEvent = await events.OfType<LoreCompleteEventData>().FirstAsync();
        Assert.True(completeEvent.Status == 0);
    }

    [Fact]
    public async Task AsyncIter_With_Filter_Does_Not_Block()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = Lore.RepositoryCreate(globalArgs, repositoryArgs)
            .FilterByType([LoreEventTag.COMPLETE])
            .AsyncIter();

        await foreach (var ev in events)
        {
            if (ev is LoreCompleteEventData completeEvent)
            {
                Assert.Equal(0, completeEvent.Status);
            }
            else
            {
                Assert.Fail("Got events other than LoreCompleteEventData.");
            }
        }
    }

    [Fact]
    public void Collect_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = Lore.RepositoryCreate(globalArgs, repositoryArgs).Collect();

        Assert.True(events.Count > 0);

        var ev = events.OfType<LoreCompleteEventData>().First();
        Assert.True(ev.Status == 0);
    }

    [Fact]
    public async Task CollectAsync_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };


        var events = await Lore.RepositoryCreate(globalArgs, repositoryArgs).CollectAsync();

        Assert.True(events.Count > 0);

        var ev = events.OfType<LoreCompleteEventData>().First();
        Assert.True(ev.Status == 0);
    }

    [Fact]
    public void Filter_By_Type_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = Lore.RepositoryCreate(globalArgs, repositoryArgs)
                        .FilterByType([LoreEventTag.LOG, LoreEventTag.COMPLETE, LoreEventTag.END])
                        .Collect();

        foreach (LoreEvent ev in events)
        {
            Assert.True(ev.Tag == LoreEventTag.LOG || ev.Tag == LoreEventTag.COMPLETE || ev.Tag == LoreEventTag.END);
        }
    }

    [Fact]
    public void User_Context_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = Lore.RepositoryCreate(globalArgs, repositoryArgs)
                        .UserContext(1234)
                        .Callback((LoreEventFFI loreEvent, ulong userContext) =>
                        {
                            Assert.Equal(1234ul, userContext);
                        })
                        .Wait();
    }

    [Fact]
    public void GlobalCallback_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryCreateArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };
        var repositoryStatusArgs = new LoreRepositoryStatusArgs { };

        var logMessages = new List<string>();

        var logMessagesAfterFirstCall = 0;
        var logMessagesAfterSecondCall = 0;
        var logMessagesAfterThirdCall = 0;

        using (Lore.GlobalCallback(LoreEventTag.LOG,
          (loreEvent, userContext) => { logMessages.Add(loreEvent.GetData<LoreLogEventDataFFI>().Message); }))
        {
            Lore.RepositoryCreate(globalArgs, repositoryCreateArgs).Wait();
            logMessagesAfterFirstCall = logMessages.Count;
            Assert.True(logMessagesAfterFirstCall > 0);

            Lore.RepositoryStatus(globalArgs, repositoryStatusArgs).Wait();
            logMessagesAfterSecondCall = logMessages.Count;
            Assert.True(logMessagesAfterSecondCall > logMessagesAfterFirstCall);
        }

        // After disposing the global callback it should no longer be executed:
        Lore.RepositoryStatus(globalArgs, repositoryStatusArgs).Wait();
        logMessagesAfterThirdCall = logMessages.Count;
        Assert.Equal(logMessagesAfterThirdCall, logMessagesAfterSecondCall);
    }

    [Fact]
    public async Task GlobalCallback_Async_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryCreateArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };
        var repositoryStatusArgs = new LoreRepositoryStatusArgs { };

        var logMessages = new List<string>();

        var logMessagesAfterFirstCall = 0;
        var logMessagesAfterSecondCall = 0;
        var logMessagesAfterThirdCall = 0;

        var unregisterGlobalCallback = Lore.GlobalCallback(LoreEventTag.LOG,
          (loreEvent, userContext) => { logMessages.Add(loreEvent.GetData<LoreLogEventDataFFI>().Message); }
        );

        await Lore.RepositoryCreate(globalArgs, repositoryCreateArgs).WaitAsync();
        logMessagesAfterFirstCall = logMessages.Count;
        Assert.True(logMessagesAfterFirstCall > 0);

        await Lore.RepositoryStatus(globalArgs, repositoryStatusArgs).WaitAsync();
        logMessagesAfterSecondCall = logMessages.Count;
        Assert.True(logMessagesAfterSecondCall > logMessagesAfterFirstCall);

        unregisterGlobalCallback.Dispose();

        // After disposing the global callback it should no longer be executed:
        await Lore.RepositoryStatus(globalArgs, repositoryStatusArgs).WaitAsync();
        logMessagesAfterThirdCall = logMessages.Count;
        Assert.Equal(logMessagesAfterThirdCall, logMessagesAfterSecondCall);
    }

    // --- Non-zero return code tests ---

    [Fact]
    public void Wait_NonZero_ReturnCode()
    {
        var invalidArgs = new LoreGlobalArgs
        {
            Offline = true,
            RepositoryPath = "/tmp/nonexistent-repo-path-" + Guid.NewGuid()
        };
        var result = Lore.RepositoryStatus(invalidArgs, new LoreRepositoryStatusArgs()).Wait();
        Assert.NotEqual(0, result);
    }

    [Fact]
    public void Collect_NonZero_ReturnCode()
    {
        var invalidArgs = new LoreGlobalArgs
        {
            Offline = true,
            RepositoryPath = "/tmp/nonexistent-repo-path-" + Guid.NewGuid()
        };
        var events = Lore.RepositoryStatus(invalidArgs, new LoreRepositoryStatusArgs()).Collect();
        var completeEvents = events.OfType<LoreCompleteEventData>().ToList();
        Assert.Single(completeEvents);
        Assert.NotEqual(0, completeEvents[0].Status);
    }

    [Fact]
    public async Task AsyncIter_NonZero_ReturnCode()
    {
        var invalidArgs = new LoreGlobalArgs
        {
            Offline = true,
            RepositoryPath = "/tmp/nonexistent-repo-path-" + Guid.NewGuid()
        };
        var events = new List<LoreEvent>();
        await foreach (var ev in Lore.RepositoryStatus(invalidArgs, new LoreRepositoryStatusArgs()).AsyncIter())
        {
            events.Add(ev);
        }
        var completeEvents = events.OfType<LoreCompleteEventData>().ToList();
        Assert.Single(completeEvents);
        Assert.NotEqual(0, completeEvents[0].Status);
    }

    // --- Double-execution prevention tests ---

    [Fact]
    public void Double_Wait_Raises()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var executor = Lore.RepositoryCreate(globalArgs, repositoryArgs);
        executor.Wait();

        Assert.Throws<InvalidOperationException>(() => executor.Wait());
    }

    [Fact]
    public void Double_Collect_Raises()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var executor = Lore.RepositoryCreate(globalArgs, repositoryArgs);
        executor.Collect();

        Assert.Throws<InvalidOperationException>(() => executor.Collect());
    }

    [Fact]
    public void Wait_Then_Collect_Raises()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var executor = Lore.RepositoryCreate(globalArgs, repositoryArgs);
        executor.Wait();

        Assert.Throws<InvalidOperationException>(() => executor.Collect());
    }

    [Fact]
    public async Task Double_AsyncIter_Raises()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };

        var executor = Lore.RepositoryCreate(globalArgs, repositoryArgs);
        await foreach (var _ in executor.AsyncIter()) { }

        // AsyncIter is an async iterator — the exception is thrown when enumeration begins
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in executor.AsyncIter()) { }
        });
    }

    [Fact]
    public async Task Wait_Then_AsyncIter_Raises()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };

        var executor = Lore.RepositoryCreate(globalArgs, repositoryArgs);
        executor.Wait();

        // AsyncIter is an async iterator — the exception is thrown when enumeration begins
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in executor.AsyncIter()) { }
        });
    }

    [Fact]
    public async Task AsyncIter_Then_Wait_Raises()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() };

        var executor = Lore.RepositoryCreate(globalArgs, repositoryArgs);
        await foreach (var _ in executor.AsyncIter()) { }

        Assert.Throws<InvalidOperationException>(() => executor.Wait());
    }

    // --- Behavioral tests ---

    [Fact]
    public void Cold_Handle_No_Execution_Until_Wait()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var callbackCalled = new List<bool>();

        var executor = Lore.RepositoryCreate(globalArgs, repositoryArgs)
            .Callback((LoreEvent, userContext) => { callbackCalled.Add(true); });

        Assert.Empty(callbackCalled);

        executor.Wait();
        Assert.NotEmpty(callbackCalled);
    }

    [Fact]
    public void Method_Chaining_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var callbackContexts = new List<ulong>();

        var result = Lore.RepositoryCreate(globalArgs, repositoryArgs)
            .Callback((LoreEvent, userContext) => { callbackContexts.Add(userContext); })
            .FilterByType([LoreEventTag.COMPLETE, LoreEventTag.END])
            .UserContext(42)
            .Wait();

        Assert.Equal(0, result);
        Assert.All(callbackContexts, ctx => Assert.Equal(42ul, ctx));
    }

    [Fact]
    public void Collect_With_Filter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = Lore.RepositoryCreate(globalArgs, repositoryArgs)
            .FilterByType([LoreEventTag.COMPLETE, LoreEventTag.END])
            .Collect();

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e is LoreCompleteEventData);
        Assert.Contains(events, e => e is LoreEndEventData);
    }

    [Fact]
    public void Collect_Event_Data_Accessible_Outside_Callback()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = Lore.RepositoryCreate(globalArgs, repositoryArgs).Collect();

        var completeEvents = events.OfType<LoreCompleteEventData>().ToList();
        Assert.Single(completeEvents);
        Assert.Equal(0, completeEvents[0].Status);

        var endEvents = events.OfType<LoreEndEventData>().ToList();
        Assert.Single(endEvents);
    }

    [Fact]
    public async Task AsyncIter_Event_Data_Accessible_Outside()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var events = new List<LoreEvent>();
        await foreach (var ev in Lore.RepositoryCreate(globalArgs, repositoryArgs).AsyncIter())
        {
            events.Add(ev);
        }

        var completeEvents = events.OfType<LoreCompleteEventData>().ToList();
        Assert.Single(completeEvents);
        Assert.Equal(0, completeEvents[0].Status);

        var endEvents = events.OfType<LoreEndEventData>().ToList();
        Assert.Single(endEvents);
    }

    [Fact]
    public async Task AsyncIter_Break_Early()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        LoreEvent? firstEvent = null;
        await foreach (var ev in Lore.RepositoryCreate(globalArgs, repositoryArgs).AsyncIter())
        {
            firstEvent = ev;
            break;
        }

        Assert.NotNull(firstEvent);
    }

    [Fact]
    public void Multiple_GlobalCallbacks_Same_Type()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var logEventsA = new List<bool>();
        var logEventsB = new List<bool>();

        using var unsubA = Lore.GlobalCallback(LoreEventTag.LOG,
            (loreEvent, userContext) => { logEventsA.Add(true); });
        using var unsubB = Lore.GlobalCallback(LoreEventTag.LOG,
            (loreEvent, userContext) => { logEventsB.Add(true); });

        Lore.RepositoryCreate(globalArgs, repositoryArgs).Wait();

        // The native event forwarder may still be mid-delivery of the final event
        // when Wait() returns (A is invoked before B for each event). The two
        // independent counts converge once it finishes that iteration.
        SpinWait.SpinUntil(() => logEventsA.Count == logEventsB.Count, TimeSpan.FromSeconds(2));

        Assert.NotEmpty(logEventsA);
        Assert.NotEmpty(logEventsB);
        Assert.Equal(logEventsA.Count, logEventsB.Count);
    }

    [Fact]
    public void GlobalCallback_Ignores_PerCall_Filter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var logEvents = new List<bool>();

        using var unsub = Lore.GlobalCallback(LoreEventTag.LOG,
            (loreEvent, userContext) => { logEvents.Add(true); });

        Lore.RepositoryCreate(globalArgs, repositoryArgs)
            .FilterByType([LoreEventTag.COMPLETE])
            .Wait();

        Assert.NotEmpty(logEvents);
    }

    [Fact]
    public void Wait_Without_Callback_Succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };
        var repositoryArgs = new LoreRepositoryCreateArgs { RepositoryUrl = repositoryUrl };

        var result = Lore.RepositoryCreate(globalArgs, repositoryArgs).Wait();
        Assert.Equal(0, result);
    }

    [Fact]
    public void Complete_And_End_Events_Emitted_For_All_Methods()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var globalArgs = new LoreGlobalArgs { Offline = true, RepositoryPath = tempDir };

        // wait + callback
        var waitEvents = new List<LoreEvent>();
        var repoUrl1 = Guid.NewGuid().ToString();
        Lore.RepositoryCreate(globalArgs, new LoreRepositoryCreateArgs { RepositoryUrl = repoUrl1 })
            .Callback((loreEvent, userContext) => { waitEvents.Add(loreEvent.Clone()); })
            .Wait();

        Assert.Contains(waitEvents, e => e is LoreCompleteEventData);
        Assert.Contains(waitEvents, e => e is LoreEndEventData);

        // collect
        var repoUrl2 = Guid.NewGuid().ToString();
        var collectEvents = Lore.RepositoryCreate(globalArgs, new LoreRepositoryCreateArgs { RepositoryUrl = repoUrl2 })
            .Collect();

        Assert.Contains(collectEvents, e => e is LoreCompleteEventData);
        Assert.Contains(collectEvents, e => e is LoreEndEventData);
    }

    [Fact]
    public async Task Multiple_Parallel_Calls()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        const int numCalls = 50;
        var tasks = new List<Task<int>>();

        for (int i = 0; i < numCalls; i++)
        {
            var repoId = Guid.NewGuid().ToString();
            var globalArgs = new LoreGlobalArgs
            {
                Offline = true,
                RepositoryPath = Path.Combine(tempDir, $"repo-{i}")
            };
            var repositoryArgs = new LoreRepositoryCreateArgs
            {
                RepositoryUrl = repoId
            };
            tasks.Add(Lore.RepositoryCreate(globalArgs, repositoryArgs).WaitAsync());
        }

        var results = await Task.WhenAll(tasks);
        Assert.Equal(numCalls, results.Length);
        Assert.All(results, r => Assert.Equal(0, r));
    }

    private static readonly byte[] StoragePartition = Enumerable.Repeat((byte)0x11, 16).ToArray();
    private static readonly byte[] StorageContext = Enumerable.Repeat((byte)0x22, 16).ToArray();

    private static ulong OpenInMemoryStoreFluent(LoreGlobalArgs globalArgs)
    {
        ulong handleId = 0;
        var openArgs = new LoreStorageOpenArgs { RepositoryPath = string.Empty, InMemory = true };

        var result = Lore.StorageOpen(globalArgs, openArgs)
            .Callback((LoreEventFFI loreEvent, ulong _) =>
            {
                if (loreEvent.Tag == LoreEventTag.STORAGE_OPENED)
                {
                    handleId = loreEvent.GetData<LoreStorageOpenedEventDataFFI>().HandleId;
                }
            })
            .Wait();

        Assert.Equal(0, result);
        Assert.NotEqual((ulong)0, handleId);
        return handleId;
    }

    [Fact]
    public void Storage_Open_Close_Fluent_Works()
    {
        var globalArgs = new LoreGlobalArgs { Offline = true };

        var handleId = OpenInMemoryStoreFluent(globalArgs);

        var closeArgs = new LoreStorageCloseArgs { Handle = new LoreStore(handleId) };
        var closeResult = Lore.StorageClose(globalArgs, closeArgs).Wait();

        Assert.Equal(0, closeResult);
    }

    [Fact]
    public void Storage_Put_Get_Fluent_Works()
    {
        var globalArgs = new LoreGlobalArgs { Offline = true };
        var handleId = OpenInMemoryStoreFluent(globalArgs);
        try
        {
            var payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();

            using var putArgs = new LoreStoragePutArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStoragePutItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(StoragePartition),
                        Context = new LoreContext(StorageContext),
                        Data = payload,
                    }
                }
            };

            var putEvents = Lore.StoragePut(globalArgs, putArgs)
                .FilterByType([LoreEventTag.STORAGE_PUT_ITEM_COMPLETE])
                .Collect()
                .OfType<LoreStoragePutItemCompleteEventData>()
                .ToList();

            Assert.Single(putEvents);
            Assert.Equal(LoreErrorCode.NONE, putEvents[0].ErrorCode);
            var putAddress = putEvents[0].Address;

            using var getArgs = new LoreStorageGetArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStorageGetItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(StoragePartition),
                        Address = putAddress,
                    }
                }
            };

            var getEvents = Lore.StorageGet(globalArgs, getArgs)
                .FilterByType([LoreEventTag.STORAGE_GET_DATA, LoreEventTag.STORAGE_GET_ITEM_COMPLETE])
                .Collect();

            var receivedBytes = getEvents
                .OfType<LoreStorageGetDataEventData>()
                .SelectMany(e => e.Bytes)
                .ToArray();
            var completes = getEvents.OfType<LoreStorageGetItemCompleteEventData>().ToList();

            Assert.Single(completes);
            Assert.Equal(LoreErrorCode.NONE, completes[0].ErrorCode);
            Assert.Equal(payload, receivedBytes);
        }
        finally
        {
            var closeArgs = new LoreStorageCloseArgs { Handle = new LoreStore(handleId) };
            Lore.StorageClose(globalArgs, closeArgs).Wait();
        }
    }

    [Fact]
    public async Task Storage_Open_Close_Fluent_WaitAsync_Works()
    {
        var globalArgs = new LoreGlobalArgs { Offline = true };

        ulong handleId = 0;
        var openArgs = new LoreStorageOpenArgs { RepositoryPath = string.Empty, InMemory = true };

        var openResult = await Lore.StorageOpen(globalArgs, openArgs)
            .Callback((LoreEventFFI loreEvent, ulong _) =>
            {
                if (loreEvent.Tag == LoreEventTag.STORAGE_OPENED)
                {
                    handleId = loreEvent.GetData<LoreStorageOpenedEventDataFFI>().HandleId;
                }
            })
            .WaitAsync();

        Assert.Equal(0, openResult);
        Assert.NotEqual((ulong)0, handleId);

        var closeArgs = new LoreStorageCloseArgs { Handle = new LoreStore(handleId) };
        var closeResult = await Lore.StorageClose(globalArgs, closeArgs).WaitAsync();
        Assert.Equal(0, closeResult);
    }

    [Fact]
    public async Task Storage_Put_Get_Fluent_AsyncIter_Works()
    {
        var globalArgs = new LoreGlobalArgs { Offline = true };
        var handleId = OpenInMemoryStoreFluent(globalArgs);
        try
        {
            var payload = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();

            using var putArgs = new LoreStoragePutArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStoragePutItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(StoragePartition),
                        Context = new LoreContext(StorageContext),
                        Data = payload,
                    }
                }
            };

            LoreAddress? putAddress = null;
            await foreach (var ev in Lore.StoragePut(globalArgs, putArgs).AsyncIter())
            {
                if (ev is LoreStoragePutItemCompleteEventData putComplete)
                {
                    Assert.Equal(LoreErrorCode.NONE, putComplete.ErrorCode);
                    putAddress = putComplete.Address;
                }
            }
            Assert.NotNull(putAddress);

            using var getArgs = new LoreStorageGetArgs
            {
                Handle = new LoreStore(handleId),
                Items = new[]
                {
                    new LoreStorageGetItem
                    {
                        Id = 1UL,
                        Partition = new LorePartition(StoragePartition),
                        Address = putAddress.Value,
                    }
                }
            };

            var receivedBytes = new List<byte>();
            var completes = 0;
            await foreach (var ev in Lore.StorageGet(globalArgs, getArgs).AsyncIter())
            {
                if (ev is LoreStorageGetDataEventData data)
                {
                    receivedBytes.AddRange(data.Bytes);
                }
                else if (ev is LoreStorageGetItemCompleteEventData getComplete)
                {
                    Assert.Equal(LoreErrorCode.NONE, getComplete.ErrorCode);
                    completes++;
                }
            }

            Assert.Equal(1, completes);
            Assert.Equal(payload, receivedBytes.ToArray());
        }
        finally
        {
            var closeArgs = new LoreStorageCloseArgs { Handle = new LoreStore(handleId) };
            await Lore.StorageClose(globalArgs, closeArgs).WaitAsync();
        }
    }
}
