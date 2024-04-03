using MyAsyncAwait.Cli;

Console.WriteLine("Hello, World!");

AsyncLocal<int> myValue = new();
List<MyTask> tasks = [];
for (var i = 0; i < 100; i++)
{
    myValue.Value = i;
    tasks.Add(MyTask.Run(() =>
    {
        Console.WriteLine(myValue.Value);
        Thread.Sleep(1000);
    }));
}
MyTask.WhenAll(tasks).Wait();

foreach (var t in tasks)
{
    t.Wait();
}