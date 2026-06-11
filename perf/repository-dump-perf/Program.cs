// Copyright Epic Games, Inc. All Rights Reserved.
//
// Performance test for native vs. fluent API event handling in the C# SDK.
//
// Mirrors:
//   - lore-js/src/perf/repository-dump.perf.ts
//   - lore-go/lore_go/cmd/perf-repository-dump/main.go
//   - lore-python/perf/repository_dump_perf.py
// so the four SDKs can be compared head-to-head on per-event FFI overhead.
//
// Run with:
//
//     dotnet run -c Release --project perf/repository-dump-perf
//
// For stabler numbers on macOS / Linux:
//
//     taskpolicy -c utility dotnet run -c Release --project perf/repository-dump-perf   # macOS
//     nice -n -19           dotnet run -c Release --project perf/repository-dump-perf   # Linux
//
// Measures the cost of consuming LORE_EVENT_REPOSITORY_STATE_DUMP_NODE events
// across four SDK access patterns:
//   1. raw native callback (LoreVcs.Interop.Native.LoreRepositoryDump)
//   2. fluent .Callback(cb).Wait()
//   3. fluent .AsyncIter() (await foreach)
//   4. fluent .Collect()
//
// Two variants per mode:
//   A. accumulate event.Size only
//   B. accumulate Name.Length, TypeData.Length, and every numeric field.
//      Variant B forces the LoreString -> string decode on the FFI paths
//      (modes 1, 2). Collect / AsyncIter paths (modes 3, 4) pay the decode
//      cost up-front during the SDK's Clone() step, so for those paths
//      variant B is just .Length on already-decoded strings.
//
// Each (mode, variant) pair runs in its OWN .NET child process so peak RSS
// can be attributed cleanly per access pattern. Within one child: warmup +
// N_RUNS measured rounds. The parent orchestrates 4 modes × 2 variants = 8
// children sequentially. The shared setup (create repo + stage 100k files +
// commit) is done once in the parent; children re-open the existing repo
// via the --child-repo flag.
//
// Trade-off: we lose per-round cross-mode interleaving (a system blip during
// one child only affects that mode's numbers). In exchange the per-mode peak
// RSS is no longer polluted by previous modes' allocations.
//
// To eliminate disk-cache variance, point the repo at a ramdisk by setting
// LORE_PERF_REPO_PARENT. Defaults to Path.GetTempPath() otherwise.
//
//     # Linux — /dev/shm is already tmpfs, no setup needed:
//     LORE_PERF_REPO_PARENT=/dev/shm dotnet run -c Release --project perf/repository-dump-perf
//
//     # macOS — create a 4 GB ramdisk once, reuse across runs, then eject:
//     diskutil erasevolume APFS perfdisk $(hdiutil attach -nomount ram://8388608)
//     LORE_PERF_REPO_PARENT=/Volumes/perfdisk dotnet run -c Release --project perf/repository-dump-perf
//     diskutil eject /Volumes/perfdisk

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LoreVcs;
using LoreVcs.Types;
using LoreVcs.Types.Args;
using LoreVcs.Types.Enums;
using LoreVcs.Types.Events;
using static LoreVcs.Interop.Native;

namespace LoreVcs.Perf;

public static class Program
{
    private const int FILE_COUNT = 100_000;
    private const int FILES_PER_LEAF_DIR = 100;
    private const int TOP_DIRS = 10;
    private const int SUB_DIRS = 100;
    private const int N_RUNS = 10;
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromMilliseconds(500);

    private const LoreEventTag NODE_TAG = LoreEventTag.REPOSITORY_STATE_DUMP_NODE;

    private static readonly string[] Modes =
    {
        "native",
        "fluent-Callback",
        "fluent-AsyncIter",
        "fluent-Collect",
    };

    private static readonly string[] Variants = { "A", "B" };

    public sealed class Pass
    {
        public ulong Events { get; set; }
        public ulong AccumulatedSize { get; set; }
        public double Ms { get; set; }
        public long RssBytes { get; set; }
        public ulong NameLenTotal { get; set; }
        public ulong TypeDataLenTotal { get; set; }
        public ulong NumericTotal { get; set; }
    }

    public sealed class ChildResult
    {
        public string Mode { get; set; } = "";
        public string Variant { get; set; } = "";
        public List<Pass> Passes { get; set; } = new();
        public long PeakRssBytes { get; set; }
    }

    private sealed class VariantResult
    {
        public required string Variant;
        public required Dictionary<string, ChildResult> PerMode;
    }

    // .NET on macOS: Process.WorkingSet64 is cached; Refresh() forces a re-read.
    // Also: Process.PeakWorkingSet64 is NOT implemented on macOS (returns 0),
    // see https://github.com/dotnet/runtime/issues/28990 — so we track our own
    // high-water mark across all CurrentRssBytes() calls.
    private static long peakRssBytes;

    private static long CurrentRssBytes()
    {
        var proc = Process.GetCurrentProcess();
        proc.Refresh();
        var ws = proc.WorkingSet64;
        if (ws > peakRssBytes) peakRssBytes = ws;
        return ws;
    }

    private static long ProcessPeakRssBytes() => peakRssBytes;

    // Force a full GC (all generations) plus LOH compaction. Called between
    // rounds in the child so each round's transient garbage doesn't leak into
    // the next round's peakRSS. The double-Collect pattern is the standard
    // .NET idiom: first collect schedules finalizers, WaitForPendingFinalizers
    // runs them, second collect reclaims what they freed.
    private static void ForceGc()
    {
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode =
            System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    private static string FmtMb(double bytes) =>
        (bytes / 1024.0 / 1024.0).ToString("F1", CultureInfo.InvariantCulture).PadLeft(6) + " MB";

    public static async Task<int> Main(string[] args)
    {
        string? childMode = null;
        string? childVariant = null;
        string? childRepo = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--child-mode": childMode = i + 1 < args.Length ? args[++i] : null; break;
                case "--child-variant": childVariant = i + 1 < args.Length ? args[++i] : null; break;
                case "--child-repo": childRepo = i + 1 < args.Length ? args[++i] : null; break;
            }
        }

        if (childMode != null)
        {
            return await RunChild(childMode, childVariant ?? "", childRepo ?? "");
        }
        return await RunParent();
    }

    // ----- child path -----------------------------------------------------

    private static async Task<int> RunChild(string mode, string variant, string repoPath)
    {
        if (!Array.Exists(Modes, m => m == mode))
        {
            throw new ArgumentException($"unknown --child-mode {mode}");
        }
        if (!Array.Exists(Variants, v => v == variant))
        {
            throw new ArgumentException($"unknown --child-variant {variant}");
        }
        if (string.IsNullOrEmpty(repoPath))
        {
            throw new ArgumentException("--child-repo required");
        }

        var globalArgs = new LoreGlobalArgs
        {
            RepositoryPath = repoPath,
            CorrelationId = $"perf-child-{mode}-{variant}",
            Offline = true,
        };

        var tag = $"[mode={mode,-22} variant={variant}]";

        // Warmup
        await Task.Delay(CooldownDuration);
        var warm = await RunMode(mode, variant, globalArgs);
        LogChild($"{tag} warmup    time={FmtMs(warm.Ms)}  events={warm.Events}  rss={FmtMb(warm.RssBytes)}");
        ForceGc();

        var passes = new List<Pass>(N_RUNS);
        for (var round = 1; round <= N_RUNS; round++)
        {
            await Task.Delay(CooldownDuration);
            var p = await RunMode(mode, variant, globalArgs);
            passes.Add(p);
            LogChild($"{tag} round={round,2} time={FmtMs(p.Ms)}  events={p.Events}  rss={FmtMb(p.RssBytes)}");
            ForceGc();
        }

        var result = new ChildResult
        {
            Mode = mode,
            Variant = variant,
            Passes = passes,
            PeakRssBytes = ProcessPeakRssBytes(),
        };
        var json = JsonSerializer.Serialize(result);
        // Plain JSON on stdout, one line. Parent captures and parses.
        Console.Out.WriteLine(json);
        Console.Out.Flush();
        return 0;
    }

    // ----- parent path ----------------------------------------------------

    private static async Task<int> RunParent()
    {
        var (globalArgs, repoPath) = await Setup();
        try
        {
            var results = new List<VariantResult>();
            foreach (var variant in Variants)
            {
                var label = variant == "A"
                    ? "(event.Size only)"
                    : "(Name.Length + TypeData.Length + numeric fields)";
                LogParent($"\n--- Variant {variant} {label}: running each mode in its own child process ---");
                var perMode = new Dictionary<string, ChildResult>();
                foreach (var mode in Modes)
                {
                    await Task.Delay(CooldownDuration);
                    perMode[mode] = SpawnChild(mode, variant, repoPath);
                }
                results.Add(new VariantResult { Variant = variant, PerMode = perMode });
            }
            foreach (var r in results) CheckConsistency(r);
            foreach (var r in results) PrintSummary(r);
        }
        finally
        {
            Teardown(globalArgs, repoPath);
        }
        return 0;
    }

    private static ChildResult SpawnChild(string mode, string variant, string repoPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath
                ?? throw new InvalidOperationException("Environment.ProcessPath is null"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            // stderr inherits, so per-round progress lines stream live to the
            // user terminal as the child writes them.
            RedirectStandardError = false,
        };
        psi.ArgumentList.Add("--child-mode");
        psi.ArgumentList.Add(mode);
        psi.ArgumentList.Add("--child-variant");
        psi.ArgumentList.Add(variant);
        psi.ArgumentList.Add("--child-repo");
        psi.ArgumentList.Add(repoPath);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Process.Start returned null");
        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"child for {mode}/{variant} exited with code {proc.ExitCode}");
        }

        // Child writes a single JSON line; if the runtime ever prints anything
        // extra to stdout we'd need a marker (cf. the Python perf script and
        // its cffi-noise wrapper), but .NET's stdout has been clean so far.
        var trimmed = stdout.Trim();
        var result = JsonSerializer.Deserialize<ChildResult>(trimmed)
            ?? throw new InvalidOperationException(
                $"failed to deserialize child stdout: {Truncate(trimmed, 500)}");
        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";

    // ----- setup ---------------------------------------------------------

    private static async Task<(LoreGlobalArgs Globals, string RepoPath)> Setup()
    {
        var parent = Environment.GetEnvironmentVariable("LORE_PERF_REPO_PARENT");
        var parentLabel = "(parent from LORE_PERF_REPO_PARENT)";
        if (string.IsNullOrEmpty(parent))
        {
            parent = Path.GetTempPath();
            parentLabel = "(parent from Path.GetTempPath())";
        }

        var repoPath = Path.Combine(parent, "lore-cs-sdk-perf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repoPath);

        var globalArgs = new LoreGlobalArgs
        {
            RepositoryPath = repoPath,
            CorrelationId = "perf-repository-dump",
            Offline = true,
        };

        LogParent($"setup: repo at {repoPath} {parentLabel}");

        var sw = Stopwatch.StartNew();
        using (var createArgs = new LoreRepositoryCreateArgs { RepositoryUrl = Guid.NewGuid().ToString() })
        {
            CheckRc("RepositoryCreate", Lore.RepositoryCreate(globalArgs, createArgs).Wait());
        }
        LogParent($"setup: RepositoryCreate done ({FmtElapsed(sw)})");

        sw = Stopwatch.StartNew();
        CreateFiles(repoPath);
        LogParent($"setup: created {FILE_COUNT} files in {TOP_DIRS * SUB_DIRS} leaf dirs ({FmtElapsed(sw)})");

        sw = Stopwatch.StartNew();
        using (var stageArgs = new LoreFileStageArgs { Paths = new[] { repoPath } })
        {
            CheckRc("FileStage", Lore.FileStage(globalArgs, stageArgs).Wait());
        }
        LogParent($"setup: FileStage done ({FmtElapsed(sw)})");

        sw = Stopwatch.StartNew();
        using (var commitArgs = new LoreRevisionCommitArgs { Message = "perf setup" })
        {
            CheckRc("RevisionCommit", Lore.RevisionCommit(globalArgs, commitArgs).Wait());
        }
        LogParent($"setup: RevisionCommit done ({FmtElapsed(sw)})");

        sw = Stopwatch.StartNew();
        using (var flushArgs = new LoreRepositoryFlushArgs())
        {
            CheckRc("RepositoryFlush", Lore.RepositoryFlush(globalArgs, flushArgs).Wait());
        }
        LogParent($"setup: RepositoryFlush done ({FmtElapsed(sw)})");

        await Task.CompletedTask;
        return (globalArgs, repoPath);
    }

    private static void Teardown(LoreGlobalArgs globalArgs, string repoPath)
    {
        try
        {
            using var flushArgs = new LoreRepositoryFlushArgs();
            Lore.RepositoryFlush(globalArgs, flushArgs).Wait();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"teardown: RepositoryFlush failed: {e.Message}");
        }
        try
        {
            if (Directory.Exists(repoPath))
            {
                Directory.Delete(repoPath, recursive: true);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"teardown: failed to remove {repoPath}: {e.Message}");
        }
    }

    private static void CreateFiles(string repoPath)
    {
        for (var top = 0; top < TOP_DIRS; top++)
        {
            for (var sub = 0; sub < SUB_DIRS; sub++)
            {
                Directory.CreateDirectory(Path.Combine(repoPath, Pad2(top), Pad2(sub)));
            }
        }
        for (var n = 0; n < FILE_COUNT; n++)
        {
            var top = n / 10_000;
            var sub = (n / FILES_PER_LEAF_DIR) % SUB_DIRS;
            var name = Pad6(n);
            var path = Path.Combine(repoPath, Pad2(top), Pad2(sub), $"{name}.txt");
            File.WriteAllText(path, name);
        }
    }

    private static string Pad2(int n) => n.ToString("D2", CultureInfo.InvariantCulture);
    private static string Pad6(int n) => n.ToString("D6", CultureInfo.InvariantCulture);

    private static void CheckRc(string op, int rc)
    {
        if (rc != 0)
        {
            throw new InvalidOperationException($"{op} returned non-zero rc={rc}");
        }
    }

    // ----- runners (one per mode, used by child) -------------------------

    private static int RunNative(LoreGlobalArgs globalArgs, string variant, Pass p)
    {
        var consumeA = variant == "A";
        var callback = new LoreEventCallbackConfig
        {
            Func = (LoreEventFFI loreEvent, ulong _) =>
            {
                if (loreEvent.Tag != NODE_TAG) return;
                var data = loreEvent.GetData<LoreRepositoryStateDumpNodeEventDataFFI>();
                if (consumeA)
                {
                    ConsumeA(p, data);
                }
                else
                {
                    ConsumeBFFI(p, data);
                }
            },
        };
        using var args = new LoreRepositoryDumpArgs();
        var sw = Stopwatch.StartNew();
        var rc = LoreRepositoryDump(globalArgs, args, callback);
        p.Ms = MsElapsed(sw);
        p.RssBytes = CurrentRssBytes();
        return rc;
    }

    private static int RunFluentCallback(LoreGlobalArgs globalArgs, string variant, Pass p)
    {
        var consumeA = variant == "A";
        using var args = new LoreRepositoryDumpArgs();
        var sw = Stopwatch.StartNew();
        var rc = Lore.RepositoryDump(globalArgs, args)
            .FilterByType(new[] { NODE_TAG })
            .Callback((LoreEventFFI loreEvent, ulong _) =>
            {
                if (loreEvent.Tag != NODE_TAG) return;
                var data = loreEvent.GetData<LoreRepositoryStateDumpNodeEventDataFFI>();
                if (consumeA)
                {
                    ConsumeA(p, data);
                }
                else
                {
                    ConsumeBFFI(p, data);
                }
            })
            .Wait();
        p.Ms = MsElapsed(sw);
        p.RssBytes = CurrentRssBytes();
        return rc;
    }

    private static async Task RunFluentAsyncIter(LoreGlobalArgs globalArgs, string variant, Pass p)
    {
        var consumeA = variant == "A";
        using var args = new LoreRepositoryDumpArgs();
        var sw = Stopwatch.StartNew();
        await foreach (var ev in Lore.RepositoryDump(globalArgs, args)
                           .FilterByType(new[] { NODE_TAG })
                           .AsyncIter())
        {
            if (ev is LoreRepositoryStateDumpNodeEventData node)
            {
                if (consumeA)
                {
                    ConsumeACloned(p, node);
                }
                else
                {
                    ConsumeBCloned(p, node);
                }
            }
        }
        p.Ms = MsElapsed(sw);
        p.RssBytes = CurrentRssBytes();
    }

    private static void RunFluentCollect(LoreGlobalArgs globalArgs, string variant, Pass p)
    {
        var consumeA = variant == "A";
        using var args = new LoreRepositoryDumpArgs();
        var sw = Stopwatch.StartNew();
        var events = Lore.RepositoryDump(globalArgs, args)
            .FilterByType(new[] { NODE_TAG })
            .Collect();
        foreach (var ev in events)
        {
            if (ev is LoreRepositoryStateDumpNodeEventData node)
            {
                if (consumeA)
                {
                    ConsumeACloned(p, node);
                }
                else
                {
                    ConsumeBCloned(p, node);
                }
            }
        }
        p.Ms = MsElapsed(sw);
        p.RssBytes = CurrentRssBytes();
    }

    private static async Task<Pass> RunMode(string mode, string variant, LoreGlobalArgs globalArgs)
    {
        var p = new Pass();
        int? rc = null;
        switch (mode)
        {
            case "native":
                rc = RunNative(globalArgs, variant, p);
                break;
            case "fluent-Callback":
                rc = RunFluentCallback(globalArgs, variant, p);
                break;
            case "fluent-AsyncIter":
                await RunFluentAsyncIter(globalArgs, variant, p);
                rc = 0;
                break;
            case "fluent-Collect":
                RunFluentCollect(globalArgs, variant, p);
                rc = 0;
                break;
            default:
                throw new InvalidOperationException($"unknown mode {mode}");
        }
        if (rc != 0)
        {
            throw new InvalidOperationException($"{mode} returned rc={rc}");
        }
        return p;
    }

    // Accumulators. Variant A touches only Size; Variant B touches every field
    // including Name and TypeData, which on the FFI path forces the
    // LoreString -> string decode.
    private static void ConsumeA(Pass p, LoreRepositoryStateDumpNodeEventDataFFI data)
    {
        p.Events++;
        p.AccumulatedSize += data.Size;
    }

    private static void ConsumeBFFI(Pass p, LoreRepositoryStateDumpNodeEventDataFFI data)
    {
        p.Events++;
        p.AccumulatedSize += data.Size;
        p.NameLenTotal += (ulong)data.Name.Length;
        p.TypeDataLenTotal += (ulong)data.TypeData.Length;
        p.NumericTotal += (ulong)data.Id + data.Parent + data.Sibling
            + (ulong)(ushort)data.Mode + data.Size + (ulong)(ushort)data.Flags;
    }

    private static void ConsumeACloned(Pass p, LoreRepositoryStateDumpNodeEventData data)
    {
        p.Events++;
        p.AccumulatedSize += data.Size;
    }

    private static void ConsumeBCloned(Pass p, LoreRepositoryStateDumpNodeEventData data)
    {
        p.Events++;
        p.AccumulatedSize += data.Size;
        p.NameLenTotal += (ulong)data.Name.Length;
        p.TypeDataLenTotal += (ulong)data.TypeData.Length;
        p.NumericTotal += (ulong)data.Id + data.Parent + data.Sibling
            + (ulong)(ushort)data.Mode + data.Size + (ulong)(ushort)data.Flags;
    }

    // ----- driver / reporting --------------------------------------------

    private static void CheckConsistency(VariantResult r)
    {
        var samples = new List<(string Mode, int Round, Pass P)>();
        foreach (var m in Modes)
        {
            if (!r.PerMode.TryGetValue(m, out var child)) continue;
            for (var i = 0; i < child.Passes.Count; i++)
            {
                samples.Add((m, i + 1, child.Passes[i]));
            }
        }
        if (samples.Count == 0) return;
        var refP = samples[0].P;
        foreach (var (mode, round, p) in samples)
        {
            if (p.Events != refP.Events)
            {
                LogParent($"  WARN variant {r.Variant} {mode} round{round}: events={p.Events} differs from reference {refP.Events}");
            }
            if (p.AccumulatedSize != refP.AccumulatedSize)
            {
                LogParent($"  WARN variant {r.Variant} {mode} round{round}: accumulatedSize={p.AccumulatedSize} differs from reference {refP.AccumulatedSize}");
            }
            if (r.Variant == "B")
            {
                if (p.NameLenTotal != refP.NameLenTotal
                    || p.TypeDataLenTotal != refP.TypeDataLenTotal
                    || p.NumericTotal != refP.NumericTotal)
                {
                    LogParent($"  WARN variant B {mode} round{round}: heavy-field accumulators differ from reference (nameLen={p.NameLenTotal}/{refP.NameLenTotal} typeDataLen={p.TypeDataLenTotal}/{refP.TypeDataLenTotal} numeric={p.NumericTotal}/{refP.NumericTotal})");
                }
            }
        }
    }

    private static void PrintSummary(VariantResult r)
    {
        var label = r.Variant == "A"
            ? "(event.Size only)"
            : "(Name.Length + TypeData.Length + numeric fields)";
        LogParent($"\n=== Variant {r.Variant} {label} — summary over {N_RUNS} runs per mode (each mode in its own child process) ===");

        var rows = new List<(string Mode, double Min, double Mean, double Max, ulong Eps, long PeakRss, ulong Events)>();
        foreach (var m in Modes)
        {
            if (!r.PerMode.TryGetValue(m, out var child) || child.Passes.Count == 0) continue;
            var min = double.PositiveInfinity;
            var max = double.NegativeInfinity;
            var sumMs = 0.0;
            ulong totalEvents = 0;
            foreach (var p in child.Passes)
            {
                if (p.Ms < min) min = p.Ms;
                if (p.Ms > max) max = p.Ms;
                sumMs += p.Ms;
                totalEvents += p.Events;
            }
            var mean = sumMs / child.Passes.Count;
            var eps = sumMs > 0 ? (ulong)(totalEvents * 1000.0 / sumMs) : 0;
            rows.Add((m, min, mean, max, eps, child.PeakRssBytes, child.Passes[0].Events));
        }

        var fastestMean = double.PositiveInfinity;
        foreach (var row in rows)
        {
            if (row.Mean < fastestMean) fastestMean = row.Mean;
        }
        foreach (var (mode, min, mean, max, eps, peakRss, events) in rows)
        {
            var ratio = mean / fastestMean;
            var epsStr = eps.ToString("N0", CultureInfo.InvariantCulture).PadLeft(9);
            var ratioStr = ratio.ToString("F2", CultureInfo.InvariantCulture);
            LogParent($"mode={mode,-24} events={events}  min={FmtMs(min)}  mean={FmtMs(mean)}  max={FmtMs(max)}  ev/s={epsStr}  peakRSS={FmtMb(peakRss)}  (mean {ratioStr}x)");
        }
    }

    // ----- formatting --------------------------------------------------

    private static void LogParent(string msg)
    {
        Console.Out.WriteLine(msg);
    }

    private static void LogChild(string msg)
    {
        // Child writes progress to stderr so stdout stays clean for JSON output.
        Console.Error.WriteLine(msg);
    }

    private static double MsElapsed(Stopwatch sw)
    {
        return sw.Elapsed.TotalMilliseconds;
    }

    private static string FmtMs(double ms)
    {
        return ms.ToString("0.0", CultureInfo.InvariantCulture).PadLeft(7) + "ms";
    }

    private static string FmtElapsed(Stopwatch sw)
    {
        return FmtMs(sw.Elapsed.TotalMilliseconds);
    }
}
