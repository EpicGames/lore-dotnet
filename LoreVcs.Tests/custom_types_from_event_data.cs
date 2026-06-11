using Xunit;
using LoreVcs.Types;
using LoreVcs.Types.Enums;
using LoreVcs.Types.Events;

namespace LoreVcs.Tests;

public class LoreCustomTypesFromEventDataTests
{
    [Fact]
    public void LoreErrorEventData_Works()
    {
        var eventData = new LoreErrorEventData
        {
            ErrorType = 0,
            ErrorInner = "LORE error"
        };

        Assert.Equal(LoreEventTag.ERROR, eventData.Tag);
        Assert.Equal((uint)0, eventData.ErrorType);
        Assert.IsType<string>(eventData.ErrorInner);
        Assert.Equal("LORE error", eventData.ErrorInner);
        Assert.Equal(10, eventData.ErrorInner.Length);
    }

    [Fact]
    public void LoreMetadataEventData_Works()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.STRING,
            @string = new LoreString("commit message")
        };
        var metadata = System.Runtime.CompilerServices.Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        var eventData = new LoreMetadataEventData
        {
            Key = "message",
            Value = metadata
        };

        Assert.Equal(LoreEventTag.METADATA, eventData.Tag);
        Assert.Equal("message", eventData.Key);
        Assert.Equal(LoreMetadataTag.STRING, eventData.Value.Tag);
        Assert.IsType<string>(eventData.Value.String);
        Assert.Equal("commit message", eventData.Value.String);
    }

    [Fact]
    public void LoreBranchCreateEventData_Works()
    {
        var hashBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

        var eventData = new LoreBranchCreateEventData
        {
            Name = "test-feature",
            Latest = hashBytes,
            IsCommit = false
        };

        Assert.Equal(LoreEventTag.BRANCH_CREATE, eventData.Tag);
        Assert.IsType<string>(eventData.Name);
        Assert.Equal("test-feature", eventData.Name);
        Assert.Equal(12, eventData.Name.Length);
        Assert.Equal(32, eventData.Latest.Length);
        Assert.Equal(
            "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
            Convert.ToHexString(eventData.Latest).ToLowerInvariant()
        );
        Assert.False(eventData.IsCommit);
    }

    [Fact]
    public void LoreBranchLatestListEntryEventData_Works()
    {
        var branchBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var revisionBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

        var eventData = new LoreBranchLatestListEntryEventData
        {
            Branch = branchBytes,
            Revision = revisionBytes
        };

        Assert.Equal(LoreEventTag.BRANCH_LATEST_LIST_ENTRY, eventData.Tag);
        Assert.Equal(16, eventData.Branch.Length);
        Assert.Equal(
            "000102030405060708090a0b0c0d0e0f",
            Convert.ToHexString(eventData.Branch).ToLowerInvariant()
        );
        Assert.Equal(32, eventData.Revision.Length);
        Assert.Equal(
            "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
            Convert.ToHexString(eventData.Revision).ToLowerInvariant()
        );
    }

    [Fact]
    public void LoreFragmentWriteEventData_Works()
    {
        var fragment = new LoreFragment
        {
            Flags = 256,
            SizePayload = 1024,
            SizeContent = 8
        };

        var eventData = new LoreFragmentWriteEventData
        {
            Fragment = fragment,
            Deduplicated = true
        };

        Assert.Equal(LoreEventTag.FRAGMENT_WRITE, eventData.Tag);
        Assert.IsType<LoreFragment>(eventData.Fragment);
        Assert.Equal((uint)256, eventData.Fragment.Flags);
        Assert.Equal((uint)1024, eventData.Fragment.SizePayload);
        Assert.Equal((ulong)8, eventData.Fragment.SizeContent);
        Assert.True(eventData.Deduplicated);
    }

    [Fact]
    public void LoreNotificationResourceUnlockedEventData_Works()
    {
        var branchBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

        var eventData = new LoreNotificationResourceUnlockedEventData
        {
            UserId = "root",
            Branch = branchBytes,
            Paths = new[] { "element1", "element2" }
        };

        Assert.Equal(LoreEventTag.NOTIFICATION_RESOURCE_UNLOCKED, eventData.Tag);
        Assert.IsType<string>(eventData.UserId);
        Assert.Equal("root", eventData.UserId);
        Assert.Equal(4, eventData.UserId.Length);
        Assert.Equal(2, eventData.Paths.Length);
        Assert.IsType<string>(eventData.Paths[0]);
        Assert.Equal("element1", eventData.Paths[0]);
        Assert.Equal("element2", eventData.Paths[1]);
    }

    [Fact]
    public void LoreLogEventData_Works()
    {
        var eventData = new LoreLogEventData
        {
            Level = LoreLogLevel.INFO,
            Category = 42,
            Timestamp = 1234567890,
            Location = "test_module",
            Message = "something happened"
        };

        Assert.Equal(LoreEventTag.LOG, eventData.Tag);
        Assert.Equal(LoreLogLevel.INFO, eventData.Level);
        Assert.Equal((uint)42, eventData.Category);
        Assert.Equal((ulong)1234567890, eventData.Timestamp);
        Assert.Equal("test_module", eventData.Location);
        Assert.Equal("something happened", eventData.Message);
    }

    [Fact]
    public void LoreRevisionInfoEventData_Works()
    {
        var repoBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var revisionBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var parent0 = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var parent1 = Enumerable.Repeat((byte)0xFF, 32).ToArray();

        var eventData = new LoreRevisionInfoEventData
        {
            Repository = repoBytes,
            Revision = revisionBytes,
            RevisionNumber = 5,
            Parent = new[] { parent0, parent1 }
        };

        Assert.Equal(LoreEventTag.REVISION_INFO, eventData.Tag);
        Assert.Equal(repoBytes, eventData.Repository);
        Assert.Equal(revisionBytes, eventData.Revision);
        Assert.Equal((ulong)5, eventData.RevisionNumber);
        Assert.Equal(2, eventData.Parent.Length);
        Assert.Equal(parent0, eventData.Parent[0]);
        Assert.Equal(parent1, eventData.Parent[1]);
    }

    [Fact]
    public void LoreStorageOpenedEventData_Works()
    {
        var eventData = new LoreStorageOpenedEventData
        {
            HandleId = 99UL
        };

        Assert.Equal(LoreEventTag.STORAGE_OPENED, eventData.Tag);
        Assert.Equal(99UL, eventData.HandleId);
    }

    [Fact]
    public void LoreStoragePutItemCompleteEventData_Works()
    {
        var hashBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var contextBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var address = new LoreAddress
        {
            Hash = new LoreHash(hashBytes),
            Context = new LoreContext(contextBytes)
        };

        var eventData = new LoreStoragePutItemCompleteEventData
        {
            Id = 1UL,
            Address = address,
            ErrorCode = LoreErrorCode.NONE
        };

        Assert.Equal(LoreEventTag.STORAGE_PUT_ITEM_COMPLETE, eventData.Tag);
        Assert.Equal(1UL, eventData.Id);
        Assert.Equal(hashBytes, eventData.Address.Hash.Data);
        Assert.Equal(contextBytes, eventData.Address.Context.Data);
        Assert.Equal(LoreErrorCode.NONE, eventData.ErrorCode);
    }

    [Fact]
    public void LoreStorageGetHeaderEventData_Works()
    {
        var hashBytes = Enumerable.Repeat((byte)0xAB, 32).ToArray();
        var contextBytes = Enumerable.Repeat((byte)0xCD, 16).ToArray();

        var eventData = new LoreStorageGetHeaderEventData
        {
            Id = 4UL,
            Address = new LoreAddress
            {
                Hash = new LoreHash(hashBytes),
                Context = new LoreContext(contextBytes)
            },
            SizeContent = 1024UL
        };

        Assert.Equal(LoreEventTag.STORAGE_GET_HEADER, eventData.Tag);
        Assert.Equal(4UL, eventData.Id);
        Assert.Equal(hashBytes, eventData.Address.Hash.Data);
        Assert.Equal(contextBytes, eventData.Address.Context.Data);
        Assert.Equal(1024UL, eventData.SizeContent);
    }

    [Fact]
    public void LoreStorageGetDataEventData_Works()
    {
        var hashBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var contextBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var payload = new byte[] { 0x10, 0x20, 0x30, 0x40 };

        var eventData = new LoreStorageGetDataEventData
        {
            Id = 5UL,
            Address = new LoreAddress
            {
                Hash = new LoreHash(hashBytes),
                Context = new LoreContext(contextBytes)
            },
            Offset = 64UL,
            Bytes = payload
        };

        Assert.Equal(LoreEventTag.STORAGE_GET_DATA, eventData.Tag);
        Assert.Equal(5UL, eventData.Id);
        Assert.Equal(hashBytes, eventData.Address.Hash.Data);
        Assert.Equal(contextBytes, eventData.Address.Context.Data);
        Assert.Equal(64UL, eventData.Offset);
        Assert.Equal(payload, eventData.Bytes);
    }

    [Fact]
    public void LoreStorageGetItemCompleteEventData_Works()
    {
        var eventData = new LoreStorageGetItemCompleteEventData
        {
            Id = 6UL,
            ErrorCode = LoreErrorCode.ADDRESS_NOT_FOUND
        };

        Assert.Equal(LoreEventTag.STORAGE_GET_ITEM_COMPLETE, eventData.Tag);
        Assert.Equal(6UL, eventData.Id);
        Assert.Equal(LoreErrorCode.ADDRESS_NOT_FOUND, eventData.ErrorCode);
    }

    [Fact]
    public void LoreStorageCopyItemCompleteEventData_Works()
    {
        var sourcePartition = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var targetPartition = Enumerable.Repeat((byte)0xEE, 16).ToArray();
        var hashBytes = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var contextBytes = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

        var eventData = new LoreStorageCopyItemCompleteEventData
        {
            Id = 7UL,
            SourcePartition = sourcePartition,
            TargetPartition = targetPartition,
            SourceAddress = new LoreAddress
            {
                Hash = new LoreHash(hashBytes),
                Context = new LoreContext(contextBytes)
            },
            ErrorCode = LoreErrorCode.NONE
        };

        Assert.Equal(LoreEventTag.STORAGE_COPY_ITEM_COMPLETE, eventData.Tag);
        Assert.Equal(7UL, eventData.Id);
        Assert.Equal(sourcePartition, eventData.SourcePartition);
        Assert.Equal(targetPartition, eventData.TargetPartition);
        Assert.Equal(hashBytes, eventData.SourceAddress.Hash.Data);
        Assert.Equal(contextBytes, eventData.SourceAddress.Context.Data);
        Assert.Equal(LoreErrorCode.NONE, eventData.ErrorCode);
    }

    [Fact]
    public void LoreStorageObliterateItemCompleteEventData_Works()
    {
        var eventData = new LoreStorageObliterateItemCompleteEventData
        {
            Id = 8UL,
            LocalSuccess = true,
            RemoteSuccess = false,
            ErrorCode = LoreErrorCode.INTERNAL
        };

        Assert.Equal(LoreEventTag.STORAGE_OBLITERATE_ITEM_COMPLETE, eventData.Tag);
        Assert.Equal(8UL, eventData.Id);
        Assert.True(eventData.LocalSuccess);
        Assert.False(eventData.RemoteSuccess);
        Assert.Equal(LoreErrorCode.INTERNAL, eventData.ErrorCode);
    }

    [Fact]
    public void LoreStorageUploadItemCompleteEventData_Works()
    {
        var eventData = new LoreStorageUploadItemCompleteEventData
        {
            Id = 11UL,
            AlreadyDurable = true,
            ErrorCode = LoreErrorCode.SLOW_DOWN
        };

        Assert.Equal(LoreEventTag.STORAGE_UPLOAD_ITEM_COMPLETE, eventData.Tag);
        Assert.Equal(11UL, eventData.Id);
        Assert.True(eventData.AlreadyDurable);
        Assert.Equal(LoreErrorCode.SLOW_DOWN, eventData.ErrorCode);
    }
}
