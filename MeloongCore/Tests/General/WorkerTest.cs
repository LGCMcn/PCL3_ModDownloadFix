namespace MeloongCore.Tests;

public class WorkerTest : TestBase {

    [Test]
    public async Task Run_在工作线程执行工作负载() {
        int callerThreadId = Thread.CurrentThread.ManagedThreadId;
        int workloadThreadId = callerThreadId;
        using var started = new ManualResetEventSlim(false);
        var worker = new RedoableWorker(() => {
            workloadThreadId = Thread.CurrentThread.ManagedThreadId;
            started.Set();
        });

        worker.Start();

        await Assert.That(started.Wait(3000)).IsTrue();
        await Assert.That(worker.WaitIfRunning(3000)).IsTrue();
        await Assert.That(workloadThreadId != callerThreadId).IsTrue();
        await Assert.That(worker.LastSucceeded).IsTrue();
    }

    [Test]
    public async Task Run_运行中再次调用只重跑一次() {
        int callerThreadId = Thread.CurrentThread.ManagedThreadId;
        int runCount = 0;
        var workloadThreadIds = new ConcurrentQueue<int>();
        using var firstStarted = new ManualResetEventSlim(false);
        var worker = new RedoableWorker(ct => {
            int currentRun = Interlocked.Increment(ref runCount);
            workloadThreadIds.Enqueue(Thread.CurrentThread.ManagedThreadId);
            if (currentRun == 1) {
                firstStarted.Set();
                while (true) {
                    ct?.ThrowIfCancellationRequested();
                    Thread.Sleep(10);
                }
            }
        });

        worker.Start();
        await Assert.That(firstStarted.Wait(3000)).IsTrue();
        worker.Start();
        worker.Start();

        await Assert.That(worker.WaitIfRunning(3000)).IsTrue();
        await Assert.That(runCount).IsEqualTo(2);
        await Assert.That(workloadThreadIds.All(id => id != callerThreadId)).IsTrue();
        await Assert.That(worker.LastSucceeded).IsTrue();
    }

}
