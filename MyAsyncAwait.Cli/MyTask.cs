using System.Runtime.ExceptionServices;

namespace MyAsyncAwait.Cli;

public class MyTask
{
    private bool _completed;
    private Exception? _exception;
    private Action? _continuation;
    private ExecutionContext? _context;

    public bool IsCompleted
    {
        get { return _completed; }
    }

    public void SetResult() => Complete(null);

    public void SetException(Exception exception)
    {
    }

    public void Wait()
    {
        ManualResetEventSlim? mres = null;
        lock (this)
        {
            if (!_completed)
            {
                mres = new ManualResetEventSlim();
                ContinueWith(mres.Set);
            }
        }

        // if completed its null and does not wait. Only  wait when a manualresetevent is created
        mres?.Wait();

        if (_exception is not null)
        {
            ExceptionDispatchInfo.Throw(_exception);
        }
    }

    public MyTask ContinueWith(Action action)
    {
        MyTask t = new();
        var callback = () =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                t.SetException(e);
                return;
            }
            t.SetResult();
        };
        lock (this)
        {
            if (_completed)
            {
                MyThreadPool.QueueUserWorkItem(callback);
            }
            else
            {
                _continuation = callback;
                _context = ExecutionContext.Capture();
            }
        }

        return t;
    }
    
    
    public MyTask ContinueWith(Func<MyTask> action)
    {
        MyTask t = new();
        var callback = () =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                t.SetException(e);
                return;
            }
            t.SetResult();
        };
        lock (this)
        {
            if (_completed)
            {
                MyThreadPool.QueueUserWorkItem(callback);
            }
            else
            {
                _continuation = callback;
                _context = ExecutionContext.Capture();
            }
        }

        return t;
    }

    public static MyTask Run(Action action)
    {
        MyTask t = new();

        MyThreadPool.QueueUserWorkItem(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                t.SetException(e);
                return;
            }

            t.SetResult();
        });
        return t;
    }

    private void Complete(Exception? exception)
    {
        lock (this)
        {
            if (_completed)
            {
                throw new InvalidOperationException("should not happen");
            }

            _completed = true;
            _exception = exception;

            if (_continuation is not null)
            {
                MyThreadPool.QueueUserWorkItem(() =>
                {
                    if (_context is null)
                    {
                        MyThreadPool.QueueUserWorkItem(_continuation);
                    }
                    else
                    {
                        ExecutionContext.Run(_context, state => ((Action)state!).Invoke(), _continuation);
                    }
                });
            }
        }
    }

    public static MyTask WhenAll(List<MyTask> tasks)
    {
        MyTask t = new();

        if (tasks.Count == 0)
        {
            t.SetResult();
        }
        else
        {
            var remaining = tasks.Count;
            var continuation = () =>
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    t.SetResult();
                }
            };
            foreach (var task in tasks)
            {
                task.ContinueWith(continuation);
            }
        }

        return t;
    }
}