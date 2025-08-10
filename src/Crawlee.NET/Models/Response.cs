using AngleSharp.Html.Dom;
using System.Collections.Generic;
using System.Net;

namespace Crawlee.NET.Models
{
    public class Response
    {
        public HttpStatusCode StatusCode { get; set; }
        public Dictionary&lt;string, string&gt; Headers { get; set; } = new();
        public string Body { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public IHtmlDocument? Html { get; set; }
        public string? Json { get; set; }
        public byte[]? Buffer { get; set; }
        public bool IsSuccess =&gt; (int)StatusCode &gt;= 200 && (int)StatusCode &lt; 300;
        public Request Request { get; set; } = new();
    }
}