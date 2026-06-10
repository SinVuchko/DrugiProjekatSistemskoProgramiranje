using System.Net;

class RequestItem
{
    public HttpListenerContext Context { get; set; }
    public string Path { get; set; }
}