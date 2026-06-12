using DrugiProjekatSistemskoProgramiranje;
using System;
using System.Net;
using System.Threading;

class Program
{
    public static volatile bool IsRunning = true;
    static async Task Main()
    {
        try
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:5050/");
            listener.Start();

            Logger.Log("Server started...");

            RequestQueue queue = new RequestQueue();
            FileCache cache = new FileCache();

            Thread cleaner = new Thread(cache.StartCleanupLoop);
            cleaner.IsBackground = true;
            cleaner.Start();

            int workerCount = 10;

            List<Task> workers = new();

            for (int i = 0; i < workerCount; i++)
            {
                workers.Add(
                    Task.Run(() => Worker.RunAsync(queue, cache))
                );
            }

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;

                Logger.Log("Graceful shutdown started...");

                Program.IsRunning = false;

                listener.Close();

                queue.Stop();
            };

            while (Program.IsRunning)
            {
                try
                {
                    var context = await listener.GetContextAsync();

                    queue.Enqueue(new RequestItem
                    {
                        Context = context,
                        Path = context.Request.Url.AbsolutePath
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log($"[MAIN ERROR] {ex.Message}");
                }
            }

            await Task.WhenAll(workers);

            Logger.Log("Server stopped.");
        }
        catch (HttpListenerException)
        {
        }
        catch (Exception ex)
        {
            Logger.Log($"[MAIN ERROR] {ex.Message}");
        }
    }
}