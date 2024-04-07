using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace MyAsyncAwait.Cli;

public class MyTask
{
    private bool _completed;
    private Exception? _exception;
    private Action? _continuation;
    private ExecutionContext? _context;

    public struct Awaiter(MyTask t) : INotifyCompletion
    {
        public Awaiter GetAwaiter() => this;
        
        public bool IsCompleted => t.IsCompleted;
        
        public void OnCompleted(Action continuation) => t.ContinueWith(continuation);
        
        public void GetResult() => t.Wait();

    }
    
    public Awaiter GetAwaiter() => new(this);

    public bool IsCompleted
    {
        get
        {
            lock (this)
            {
                return _completed;
            }
        }
    }

    public void SetResult() => Complete(null);

    public void SetException(Exception exception) => Complete(exception);

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


    public MyTask ContinueWith(Func<MyTask> func)
    {
        MyTask t = new();
        var callback = () =>
        {
            try
            {
                var next = func();
                next.ContinueWith(() =>
                {
                    if (next._exception is not null)
                    {
                        t.SetException(next._exception);
                    }
                    else
                    {
                        t.SetResult();
                    }
                });
            }
            catch (Exception e)
            {
                t.SetException(e);
                return;
            }
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
                        _continuation();
                    }
                    else
                    {
                        ExecutionContext.Run(_context, state => ((Action)state!).Invoke(), _continuation);
                    }
                });
            }
        }
    }

    public static MyTask Delay(int timeout)
    {
        MyTask t = new();
        new Timer(_ => t.SetResult()).Change(timeout, -1);
        return t;
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

    public static MyTask Iterate(IEnumerable<MyTask> tasks)
    {
        MyTask t = new();

        using var enumerator = tasks.GetEnumerator();
        MoveNext(enumerator, t);
        return t;
    }

    private static void MoveNext(IEnumerator<MyTask> enumerator, MyTask t)
    {
        try
        {
            if (enumerator.MoveNext())
            {
                var next = enumerator.Current;
                next.ContinueWith(() => MoveNext(enumerator, t));
                return;
            }
        }
        catch (Exception exception)
        {
            t.SetException(exception);
            return;
        }

        t.SetResult();
    }
}