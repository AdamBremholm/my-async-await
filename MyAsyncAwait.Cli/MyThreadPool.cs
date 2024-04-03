using System.Collections.Concurrent;

namespace MyAsyncAwait.Cli;

public static class MyThreadPool
{
    private static readonly BlockingCollection<(Action action, ExecutionContext? executionContext)> SWorkItems = new();

    public static void QueueUserWorkItem(Action action) => SWorkItems.Add((action, ExecutionContext.Capture()));

    static MyThreadPool()
    {
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            new Thread(() =>
            {
                while (true)
                {
                    var (workItem, context) = SWorkItems.Take();
                    if (context is null)
                    {
                        workItem();
                    }
                    else
                    {
                        ExecutionContext.Run(context, state => ((Action)state!).Invoke(), workItem);
                    }
                }
            }) { IsBackground = false }.Start();
        }
    }
}