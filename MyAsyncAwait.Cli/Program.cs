using MyAsyncAwait.Cli;

AsyncLocal<int> myValue = new();
List<MyTask> tasks = [];
// for (var i = 0; i < 100; i++)
// {
//     myValue.Value = i;
//     tasks.Add(MyTask.Run(() =>
//     {
//         Console.WriteLine(myValue.Value);
//         Thread.Sleep(1000);
//     }));
// }

// Console.WriteLine("Hello, ");
// MyTask.Delay(2000).ContinueWith(delegate
// {
//     Console.WriteLine("World");
//     return MyTask.Delay(2000).ContinueWith(delegate
//     {
//         Console.WriteLine(" And Scott");
//         return MyTask.Delay(2000).ContinueWith(delegate
//         {
//             Console.WriteLine(" How are you?");
//         });
//     });
// }).Wait();

MyTask.Iterate(PrintAsync()).Wait();

static IEnumerable<MyTask> PrintAsync()
{
    for (int i = 0;  ; i++)
    {
        yield return MyTask.Delay(1000);
        Console.WriteLine(i);
    }
}

// MyTask.WhenAll(tasks).Wait();

// foreach (var t in tasks)
// {
//     t.Wait();
// }