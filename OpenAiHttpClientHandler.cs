namespace AutoGen.BasicSamples;

/// <summary>
/// 重写SemanticKernel的请求处理
/// </summary>
public sealed class OpenAiHttpClientHandler : HttpClientHandler
{
    public OpenAiHttpClientHandler()
    {
    }
    
    public OpenAiHttpClientHandler(string url)
    {
        _url = url;
    }
    
    private readonly string _url = "http://localhost:11434";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.RequestUri = new UriBuilder(_url + request.RequestUri?.LocalPath).Uri;
        var response = await base.SendAsync(request, cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}