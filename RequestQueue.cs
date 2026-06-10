using DrugiProjekatSistemskoProgramiranje;
using System;
using System.Collections.Generic;
using System.Threading;

class RequestQueue
{
    private Queue<RequestItem> queue = new Queue<RequestItem>();
    private object lockObj = new object();
    private bool stopped = false;

    public void Enqueue(RequestItem item)
    {
        try
        {
            lock (lockObj)
            {
                queue.Enqueue(item);
                Monitor.Pulse(lockObj);
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[QUEUE ENQUEUE ERROR] {ex.Message}");
        }
    }

    public RequestItem Dequeue()
    {
        try
        {
            lock (lockObj)
            {
                while (queue.Count == 0 && !stopped)
                {
                    Monitor.Wait(lockObj);
                }

                if (stopped && queue.Count == 0)
                {
                    return null;
                }

                return queue.Dequeue();
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"[QUEUE DEQUEUE ERROR] {ex.Message}");
            return null;
        }
    }

    public void Stop()
    {
        lock (lockObj)
        {
            stopped = true;
            Monitor.PulseAll(lockObj);
        }
    }
}