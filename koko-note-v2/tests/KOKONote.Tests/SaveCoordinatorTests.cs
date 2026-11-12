using KOKONote.Core;

namespace KOKONote.Tests;

public class SaveCoordinatorTests
{
    [Fact]
    public async Task EditWhileSaveRunsIsNotAcknowledgedOrOverwritten()
    {
        var w = new Workspace(new([], SessionState.Empty)); var n = w.Create();
        var store = new BlockingStore(); var saver = new SaveCoordinator(w, store);
        w.Edit(n.Id, content: "first");
        var first = saver.SaveAsync();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        w.Edit(n.Id, content: "newer");
        store.Release.Set(); await first;
        Assert.True(w.IsDirty);
        await saver.SaveAsync();
        Assert.False(w.IsDirty);
        Assert.Equal("newer", Assert.Single(store.Latest!.Notes).Content);
    }

    [Fact]
    public async Task FailedSaveRetainsGenerationAndRetryWritesLatest()
    {
        var w = new Workspace(new([], SessionState.Empty)); var n = w.Create();
        var store = new FailingStore(); var saver = new SaveCoordinator(w, store);
        w.Edit(n.Id, content: "保持する本文");
        await Assert.ThrowsAsync<StorageException>(saver.SaveAsync);
        Assert.True(w.IsDirty); Assert.Equal("保持する本文", w.Notes[0].Content);
        store.Fail = false; await saver.SaveAsync();
        Assert.False(w.IsDirty); Assert.Equal("保持する本文", store.Latest!.Notes[0].Content);
    }

    private sealed class BlockingStore : INoteStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public SaveBatch? Latest;
        public StoredWorkspace Load() => throw new NotSupportedException();
        public void Save(SaveBatch batch) { Entered.TrySetResult(); if (!Release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException(); Latest = batch; }
    }
    private sealed class FailingStore : INoteStore
    {
        public bool Fail = true;
        public SaveBatch? Latest;
        public StoredWorkspace Load() => throw new NotSupportedException();
        public void Save(SaveBatch batch) { if (Fail) throw new StorageException(StorageFailure.Database); Latest = batch; }
    }
}
