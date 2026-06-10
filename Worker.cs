using DrugiProjekatSistemskoProgramiranje;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

class Worker
{
    private static SemaphoreSlim semaphore =
    new SemaphoreSlim(10);
    public static async Task RunAsync(RequestQueue queue, FileCache cache)
    {

        while (Program.IsRunning)
        {
            await semaphore.WaitAsync();

            RequestItem req = null;

            try
            {
                req = queue.Dequeue();

                if (req == null)
                    break;

                string path = req.Path.Trim('/');

                if (!string.IsNullOrEmpty(path) && Path.HasExtension(path))
                {
                    await HandleFileDownload(req.Context,path,cache);
                }
                else
                {
                    string response =await cache.GetAsync(path,() => Task.FromResult(BuildHtml(path)));

                    byte[] buffer = Encoding.UTF8.GetBytes(response);

                    req.Context.Response.ContentType = "text/html; charset=utf-8";
                    req.Context.Response.ContentLength64 = buffer.Length;

                    req.Context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[WORKER ERROR] {ex.Message}");

                if (req != null)
                    req.Context.Response.StatusCode = 500;
            }
            finally
            {
                if (req != null)
                    req.Context.Response.Close();
            }
            semaphore.Release();
        }
    }

    private static async Task HandleFileDownload(HttpListenerContext context,string fileName,FileCache cache)
    {
        try
        {
            if (fileName.Contains(".."))
            {
                context.Response.StatusCode = 400;
                return;
            }

            Task<string> loadTask =cache.GetAsync(fileName,() => LoadFileAsBase64Async(fileName));

            loadTask.ContinueWith(t =>
            {
                Logger.Log(
                    $"Loaded {fileName}"
                );
            });

            string base64 = await loadTask;

            if (string.IsNullOrEmpty(base64))
            {
                context.Response.StatusCode = 404;
                return;
            }

            byte[] fileBytes = Convert.FromBase64String(base64);

            context.Response.ContentType = "application/octet-stream";
            context.Response.ContentLength64 = fileBytes.Length;

            context.Response.AddHeader(
                "Content-Disposition",
                $"attachment; filename=\"{fileName}\""
            );

            context.Response.OutputStream.WriteAsync(fileBytes, 0, fileBytes.Length);

            Logger.Log($"[DOWNLOAD CACHE] {fileName}");
        }
        catch (Exception ex)
        {
            Logger.Log($"[DOWNLOAD ERROR] {ex.Message}");
            context.Response.StatusCode = 500;
        }
    }

    private static async Task<string> LoadFileAsBase64Async(string fileName)
    {
        try
        {
            string root = Directory.GetCurrentDirectory();
            string fullPath = Path.Combine(root, fileName);

            if (!File.Exists(fullPath))
                return null;

            byte[] data = await File.ReadAllBytesAsync(fullPath);
            return Convert.ToBase64String(data);
        }
        catch (Exception ex)
        {
            Logger.Log($"[LOAD FILE ERROR] {ex.Message}");
            return null;
        }
    }

    private static string BuildHtml(string search)
    {
        try
        {
            string root = Directory.GetCurrentDirectory();

            var files = Directory.GetFiles(root)
                .Where(f => Path.GetFileName(f)
                .Contains(search ?? "", StringComparison.OrdinalIgnoreCase));

            StringBuilder html = new StringBuilder();

            html.Append("<html><body><ul>");

            foreach (var f in files)
            {
                string name = Path.GetFileName(f);
                html.Append($"<li><a href=\"/{name}\">{name}</a></li>");
            }

            html.Append("</ul></body></html>");

            return html.ToString();
        }
        catch (Exception ex)
        {
            Logger.Log($"[HTML BUILD ERROR] {ex.Message}");
            return "<html><body>Error</body></html>";
        }
    }
}