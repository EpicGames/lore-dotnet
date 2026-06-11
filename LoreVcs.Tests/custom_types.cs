using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
using LoreVcs.Types;
using LoreVcs.Types.Enums;

namespace LoreVcs.Tests;

/// <summary>
/// Helper struct that mirrors LoreMetadata layout but with public fields,
/// allowing test code to construct LoreMetadata instances for the union type
/// which only has read-only properties.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
internal struct LoreMetadataBuilder
{
    [FieldOffset(0)]
    public LoreMetadataTag tag;

    [FieldOffset(8)]
    public LoreAddress address;

    [FieldOffset(8)]
    public byte boolean;

    [FieldOffset(8)]
    public LoreBinary binary;

    [FieldOffset(8)]
    public LoreContext context;

    [FieldOffset(8)]
    public LoreHash hash;

    [FieldOffset(8)]
    public ulong numeric;

    [FieldOffset(8)]
    public LoreString @string;
}

public class LoreCustomTypesTests
{
    private static readonly byte[] TestContext = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
    private static readonly byte[] TestHash = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] TestBinary = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();

    [Fact]
    public void LoreMetadataTypeArray_Works()
    {
        var input = new[] { LoreMetadataType.BINARY, LoreMetadataType.NUMERIC, LoreMetadataType.STRING };
        var arr = new LoreMetadataTypeArray(input);
        var native = LoreMetadataTypeArray.ToNative(arr);

        Assert.Equal(3, native.Length);
        Assert.Equal(LoreMetadataType.BINARY, native[0]);
        Assert.Equal(LoreMetadataType.NUMERIC, native[1]);
        Assert.Equal(LoreMetadataType.STRING, native[2]);

        arr.Dispose();
    }

    [Fact]
    public void LoreHash_Bytes()
    {
        var hash = new LoreHash(TestHash);
        var data = hash.Data;

        for (int i = 0; i < 32; i++)
        {
            Assert.Equal((byte)i, data[i]);
        }
    }

    [Fact]
    public void LoreUint32Array_Works()
    {
        var input = Enumerable.Range(0, 24).Select(i => (uint)i).ToArray();
        var arr = new LoreUint32Array(input);
        var native = LoreUint32Array.ToNative(arr);

        Assert.Equal(24, native.Length);
        for (int i = 0; i < 24; i++)
        {
            Assert.Equal((uint)i, native[i]);
        }

        arr.Dispose();
    }

    [Fact]
    public void LoreUint8Array_Works()
    {
        var input = new[] { true, false, true, true, false };
        var arr = new LoreUint8Array(input);
        var native = LoreUint8Array.ToNative(arr);

        Assert.Equal(5, native.Length);
        Assert.True(native[0]);
        Assert.False(native[1]);
        Assert.True(native[2]);
        Assert.True(native[3]);
        Assert.False(native[4]);

        arr.Dispose();
    }

    [Fact]
    public void LoreAddress_Properties()
    {
        var address = new LoreAddress
        {
            Hash = new LoreHash(TestHash),
            Context = new LoreContext(TestContext)
        };

        Assert.IsType<LoreHash>(address.Hash);
        Assert.IsType<LoreContext>(address.Context);
        Assert.Equal(TestHash, address.Hash.Data);
        Assert.Equal(TestContext, address.Context.Data);
    }

    [Fact]
    public void LoreAddress_Default_Constructor()
    {
        var address = new LoreAddress();

        Assert.Equal(new byte[32], address.Hash.Data);
        Assert.Equal(new byte[16], address.Context.Data);
    }

    [Fact]
    public void LoreBinary_Works()
    {
        var binary = new LoreBinary(TestBinary);
        var native = LoreBinary.ToNative(binary);

        Assert.Equal(10, native.Length);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal((byte)i, native[i]);
        }

        binary.Dispose();
    }

    [Fact]
    public unsafe void LoreMetadata_Address()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.ADDRESS,
            address = new LoreAddress
            {
                Hash = new LoreHash(TestHash),
                Context = new LoreContext(TestContext)
            }
        };
        var metadata = Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        Assert.Equal(LoreMetadataTag.ADDRESS, metadata.Tag);
        Assert.IsType<LoreAddress>(metadata.Address);
        Assert.IsType<LoreHash>(metadata.Address.Hash);
        Assert.IsType<LoreContext>(metadata.Address.Context);
    }

    [Fact]
    public unsafe void LoreMetadata_Binary()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.BINARY,
            binary = new LoreBinary(TestBinary)
        };
        var metadata = Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        Assert.Equal(LoreMetadataTag.BINARY, metadata.Tag);
        Assert.Equal(10, metadata.Binary.Length);
    }

    [Fact]
    public unsafe void LoreMetadata_Boolean()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.BOOLEAN,
            boolean = 1
        };
        var metadata = Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        Assert.Equal(LoreMetadataTag.BOOLEAN, metadata.Tag);
        Assert.True(metadata.Boolean);
    }

    [Fact]
    public unsafe void LoreMetadata_Context()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.CONTEXT,
            context = new LoreContext(TestContext)
        };
        var metadata = Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        Assert.Equal(LoreMetadataTag.CONTEXT, metadata.Tag);
        Assert.IsType<LoreContext>(metadata.Context);
        Assert.Equal(16, metadata.Context.Data.Length);
    }

    [Fact]
    public unsafe void LoreMetadata_Hash()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.HASH,
            hash = new LoreHash(TestHash)
        };
        var metadata = Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        Assert.Equal(LoreMetadataTag.HASH, metadata.Tag);
        Assert.IsType<LoreHash>(metadata.Hash);
        Assert.Equal(32, metadata.Hash.Data.Length);
    }

    [Fact]
    public unsafe void LoreMetadata_Numeric()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.NUMERIC,
            numeric = 1234
        };
        var metadata = Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        Assert.Equal(LoreMetadataTag.NUMERIC, metadata.Tag);
        Assert.Equal((ulong)1234, metadata.Numeric);
    }

    [Fact]
    public unsafe void LoreMetadata_String()
    {
        var builder = new LoreMetadataBuilder
        {
            tag = LoreMetadataTag.STRING,
            @string = new LoreString("mystring")
        };
        var metadata = Unsafe.As<LoreMetadataBuilder, LoreMetadata>(ref builder);

        Assert.Equal(LoreMetadataTag.STRING, metadata.Tag);
        Assert.IsType<string>(metadata.String);
        Assert.Equal("mystring", metadata.String);
    }

    [Fact]
    public void LoreFragment_WithArgs()
    {
        var fragment = new LoreFragment
        {
            Flags = 1,
            SizePayload = 2,
            SizeContent = 3
        };

        Assert.Equal((uint)1, fragment.Flags);
        Assert.Equal((uint)2, fragment.SizePayload);
        Assert.Equal((ulong)3, fragment.SizeContent);
    }

    [Fact]
    public void LoreFragment_UnsetProperties_Default()
    {
        var fragment = new LoreFragment { Flags = 1 };

        Assert.Equal((uint)1, fragment.Flags);
        Assert.Equal((uint)0, fragment.SizePayload);
        Assert.Equal((ulong)0, fragment.SizeContent);
    }

    [Fact]
    public void LoreFragment_Default_Constructor()
    {
        var fragment = new LoreFragment();

        Assert.Equal((uint)0, fragment.Flags);
        Assert.Equal((uint)0, fragment.SizePayload);
        Assert.Equal((ulong)0, fragment.SizeContent);
    }

    [Fact]
    public void LoreBranchPoint_Properties()
    {
        var branchPoint = new LoreBranchPoint
        {
            Branch = new LoreBranchId(TestContext),
            Revision = new LoreHash(TestHash)
        };

        Assert.IsType<LoreBranchId>(branchPoint.Branch);
        Assert.IsType<LoreHash>(branchPoint.Revision);
        Assert.Equal(TestContext, branchPoint.Branch.Data);
        Assert.Equal(TestHash, branchPoint.Revision.Data);
    }

    [Fact]
    public void LoreBranchPoint_Default_Constructor()
    {
        var branchPoint = new LoreBranchPoint();

        Assert.Equal(new byte[16], branchPoint.Branch.Data);
        Assert.Equal(new byte[32], branchPoint.Revision.Data);
    }

    [Fact]
    public void LoreHash_HexEncoding()
    {
        var hash = new LoreHash(TestHash);
        var hexStr = Convert.ToHexString(hash.Data).ToLowerInvariant();

        var expected = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
        Assert.Equal(expected, hexStr);
        Assert.Equal(64, hexStr.Length);

        var allZeros = new LoreHash(new byte[32]);
        Assert.Equal(new string('0', 64), Convert.ToHexString(allZeros.Data).ToLowerInvariant());

        var allFf = new LoreHash(Enumerable.Repeat((byte)0xFF, 32).ToArray());
        Assert.Equal(string.Concat(Enumerable.Repeat("ff", 32)), Convert.ToHexString(allFf.Data).ToLowerInvariant());
    }

    [Fact]
    public void LoreContext_HexEncoding()
    {
        var context = new LoreContext(TestContext);
        var hexStr = Convert.ToHexString(context.Data).ToLowerInvariant();

        var expected = "000102030405060708090a0b0c0d0e0f";
        Assert.Equal(expected, hexStr);
        Assert.Equal(32, hexStr.Length);

        var allZeros = new LoreContext(new byte[16]);
        Assert.Equal(new string('0', 32), Convert.ToHexString(allZeros.Data).ToLowerInvariant());

        var allFf = new LoreContext(Enumerable.Repeat((byte)0xFF, 16).ToArray());
        Assert.Equal(string.Concat(Enumerable.Repeat("ff", 16)), Convert.ToHexString(allFf.Data).ToLowerInvariant());
    }

    [Fact]
    public void LoreBranchDiffNodeData_Works()
    {
        var node = new LoreBranchDiffNodeData
        {
            Action = LoreFileAction.ADD,
            Path = "src/main.cs"
        };

        Assert.Equal(LoreFileAction.ADD, node.Action);
        Assert.Equal("src/main.cs", node.Path);

        // Struct copy (equivalent to Python clone)
        var copy = node;
        Assert.Equal(LoreFileAction.ADD, copy.Action);
        Assert.Equal("src/main.cs", copy.Path);
    }

    [Fact]
    public void LoreBranchSwitchData_Works()
    {
        var branchId = TestContext;
        var localHash = TestHash;
        var remoteHash = Enumerable.Repeat((byte)0xFF, 32).ToArray();
        var revHash = Enumerable.Repeat((byte)0xAA, 32).ToArray();

        var data = new LoreBranchSwitchData
        {
            Id = new LoreBranchId(branchId),
            Name = "feature-branch",
            LatestLocal = new LoreHash(localHash),
            LatestRemote = new LoreHash(remoteHash),
            Revision = new LoreHash(revHash),
            Location = LoreBranchLocation.REMOTE
        };

        Assert.IsType<LoreBranchId>(data.Id);
        Assert.Equal(branchId, data.Id.Data);
        Assert.Equal("feature-branch", data.Name);
        Assert.IsType<LoreHash>(data.LatestLocal);
        Assert.Equal(localHash, data.LatestLocal.Data);
        Assert.IsType<LoreHash>(data.LatestRemote);
        Assert.Equal(remoteHash, data.LatestRemote.Data);
        Assert.IsType<LoreHash>(data.Revision);
        Assert.Equal(revHash, data.Revision.Data);
        Assert.Equal(LoreBranchLocation.REMOTE, data.Location);

        // Struct copy (equivalent to Python clone)
        var copy = data;
        Assert.Equal("feature-branch", copy.Name);
        Assert.Equal(branchId, copy.Id.Data);
        Assert.Equal(localHash, copy.LatestLocal.Data);
        Assert.Equal(remoteHash, copy.LatestRemote.Data);
        Assert.Equal(revHash, copy.Revision.Data);
        Assert.Equal(LoreBranchLocation.REMOTE, copy.Location);
    }

    [Fact]
    public void LoreHashArray_ToNative()
    {
        var hash1 = TestHash;
        var hash2 = Enumerable.Repeat((byte)0xFF, 32).ToArray();

        var arr = new LoreHashArray();
        arr[0] = new LoreHash(hash1);
        arr[1] = new LoreHash(hash2);

        var native = LoreHashArray.ToNative(arr);

        Assert.Equal(2, native.Length);
        Assert.Equal(hash1, native[0]);
        Assert.Equal(hash2, native[1]);
    }

    [Fact]
    public void LoreInstanceIdArray_ToNative()
    {
        var id1 = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var id2 = Enumerable.Repeat((byte)0xFF, 16).ToArray();

        var arr = new LoreInstanceIdArray(new[] { id1, id2 });
        var native = LoreInstanceIdArray.ToNative(arr);

        Assert.Equal(2, native.Length);
        Assert.Equal(id1, native[0]);
        Assert.Equal(id2, native[1]);

        arr.Dispose();
    }

    [Fact]
    public void LoreStore_Constructor()
    {
        var store = new LoreStore(42UL);

        Assert.Equal(42UL, store.HandleId);
    }

    [Fact]
    public void LoreStore_ImplicitConversion()
    {
        LoreStore store = 7UL;

        Assert.Equal(7UL, store.HandleId);
    }

    [Fact]
    public void LoreStore_Invalid_Sentinel()
    {
        Assert.Equal(0UL, LoreStore.Invalid.HandleId);
        Assert.Equal(0UL, default(LoreStore).HandleId);
    }

    [Fact]
    public void LoreBytes_Roundtrip()
    {
        var input = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();
        var bytes = new LoreBytes(input);
        var native = LoreBytes.ToNative(bytes);

        Assert.Equal(input.Length, native.Length);
        Assert.Equal(input, native);

        bytes.Dispose();
    }

    [Fact]
    public void LoreBytes_ImplicitConversion()
    {
        var input = new byte[] { 1, 2, 3, 4 };
        LoreBytes bytes = input;

        Assert.Equal(input, LoreBytes.ToNative(bytes));

        bytes.Dispose();
    }

    [Fact]
    public void LoreBytes_Empty()
    {
        var bytes = new LoreBytes(Array.Empty<byte>());
        var native = LoreBytes.ToNative(bytes);

        Assert.Empty(native);

        bytes.Dispose();
    }

    [Fact]
    public void LoreBytes_NullInput()
    {
        var bytes = new LoreBytes(null!);
        var native = LoreBytes.ToNative(bytes);

        Assert.Empty(native);

        bytes.Dispose();
    }

    [Fact]
    public void LoreStoragePutItem_Properties()
    {
        var payload = new byte[] { 9, 8, 7, 6, 5 };
        var item = new LoreStoragePutItem
        {
            Id = 1UL,
            Partition = new LorePartition(TestContext),
            Context = new LoreContext(TestContext),
            Data = payload,
            RemoteWrite = true,
        };

        Assert.Equal(1UL, item.Id);
        Assert.Equal(TestContext, item.Partition.Data);
        Assert.Equal(TestContext, item.Context.Data);
        Assert.Equal(payload, item.Data);
        Assert.True(item.RemoteWrite);

        item.Dispose();
    }

    [Fact]
    public void LoreStorageGetItem_Properties()
    {
        var item = new LoreStorageGetItem
        {
            Id = 2UL,
            Partition = new LorePartition(TestContext),
            Address = new LoreAddress
            {
                Hash = new LoreHash(TestHash),
                Context = new LoreContext(TestContext)
            },
            Streaming = true,
        };

        Assert.Equal(2UL, item.Id);
        Assert.Equal(TestContext, item.Partition.Data);
        Assert.Equal(TestHash, item.Address.Hash.Data);
        Assert.Equal(TestContext, item.Address.Context.Data);
        Assert.True(item.Streaming);

        item.Dispose();
    }

    [Fact]
    public void LoreStoragePutItemArray_Roundtrip()
    {
        var payload0 = new byte[] { 1, 2, 3 };
        var payload1 = new byte[] { 4, 5, 6, 7 };
        var input = new[]
        {
            new LoreStoragePutItem { Id = 1UL, Data = payload0 },
            new LoreStoragePutItem { Id = 2UL, Data = payload1 },
        };

        var arr = new LoreStoragePutItemArray(input);
        var native = LoreStoragePutItemArray.ToNative(arr);

        Assert.Equal(2, native.Length);
        Assert.Equal(1UL, native[0].Id);
        Assert.Equal(payload0, native[0].Data);
        Assert.Equal(2UL, native[1].Id);
        Assert.Equal(payload1, native[1].Data);

        arr.Dispose();
    }

    [Fact]
    public void LoreStorageGetItemArray_Roundtrip()
    {
        var input = new[]
        {
            new LoreStorageGetItem { Id = 10UL, Streaming = false },
            new LoreStorageGetItem { Id = 11UL, Streaming = true },
        };

        var arr = new LoreStorageGetItemArray(input);
        var native = LoreStorageGetItemArray.ToNative(arr);

        Assert.Equal(2, native.Length);
        Assert.Equal(10UL, native[0].Id);
        Assert.False(native[0].Streaming);
        Assert.Equal(11UL, native[1].Id);
        Assert.True(native[1].Streaming);

        arr.Dispose();
    }
}
