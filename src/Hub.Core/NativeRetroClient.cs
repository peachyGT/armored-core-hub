using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Hub.Core;

// Native rcheevos APIs use a login token, not a user's Web API key. Requests are
// POST-only to the official endpoint; transport/response errors never echo secrets.
public sealed class NativeRetroClient : IDisposable
{
    private readonly HubPaths paths;private readonly HttpClient client;private readonly bool ownsClient;
    public NativeRetroClient(HubPaths paths,HttpClient? client=null)
    {
        this.paths=paths;this.client=client??new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(30)};ownsClient=client==null;
        if(ownsClient)this.client.DefaultRequestHeaders.UserAgent.ParseAdd("ArmoredCoreHub/0.3.9");
    }
    public async Task SignInAsync(string username,string password,bool offline,CancellationToken ct=default)
    {
        if(offline)throw new HubException("Go online in the Hub before signing in to RetroAchievements.");
        if(string.IsNullOrWhiteSpace(username)||username.Length>64||username.Any(char.IsControl)||password is null||password.Length is <1 or >1024)throw new HubException("Enter your RetroAchievements username and password in the Hub.");
        var bytes=await PostAsync(new(){{"r","login2"},{"u",username},{"p",password}},64*1024,ct);
        try
        {
            using var json=JsonDocument.Parse(bytes);var root=json.RootElement;RequireSuccess(root,"Sign-in failed. Check your username and password, or try again later.");
            var account=new NativeAchievementAccount{Username=root.GetProperty("User").GetString()??"",Token=root.GetProperty("Token").GetString()??"",SignedInAt=DateTimeOffset.UtcNow};NativeAchievementAccounts.Validate(account);ct.ThrowIfCancellationRequested();NativeAchievementAccounts.Save(paths,account);
        }
        catch(Exception e)when(e is JsonException or KeyNotFoundException or InvalidOperationException){throw new HubException("RetroAchievements returned an unreadable sign-in response. Try again later.");}
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    public async Task<(byte[] Patch,byte[] Soft,byte[] Hardcore,NativeAchievementAccount Account)> ReadProgressAsync(int gameId,bool offline,CancellationToken ct=default)
    {
        if(offline)throw new HubException("Go online to refresh RetroAchievements. Cached progress is still available.");
        var account=NativeAchievementAccounts.Load(paths)??throw new HubException("Sign in to RetroAchievements in the Hub first.");if(gameId<1)throw new HubException("This game has no verified RetroAchievements ID yet.");
        Dictionary<string,string> Request(string operation)=>new(){{"r",operation},{"u",account.Username},{"t",account.Token},{"g",gameId.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
        var patch=await PostAsync(Request("patch"),8*1024*1024,ct);var softRequest=Request("unlocks");softRequest["h"]="0";var soft=await PostAsync(softRequest,256*1024,ct);var hardRequest=Request("unlocks");hardRequest["h"]="1";var hard=await PostAsync(hardRequest,256*1024,ct);
        return(patch,soft,hard,account);
    }
    private async Task<byte[]> PostAsync(Dictionary<string,string> values,int maximum,CancellationToken ct)
    {
        try
        {
            using var content=new FormUrlEncodedContent(values);using var request=new HttpRequestMessage(HttpMethod.Post,"https://retroachievements.org/dorequest.php"){Content=content};
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if(!response.IsSuccessStatusCode)throw new HubException(response.StatusCode==System.Net.HttpStatusCode.TooManyRequests?"RetroAchievements asked to slow down. Wait a moment and try again.":"RetroAchievements could not complete the request. Check your connection or sign in again.");
            if(response.Content.Headers.ContentLength>maximum)throw new HubException("RetroAchievements returned a response larger than its limit.");
            await using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[8192];int read;
            try
            {
                while((read=await input.ReadAsync(buffer,ct))>0){if(output.Length+read>maximum)throw new HubException("RetroAchievements returned a response larger than its limit.");output.Write(buffer,0,read);}
                return output.ToArray();
            }
            finally{CryptographicOperations.ZeroMemory(buffer);if(output.TryGetBuffer(out var stored))CryptographicOperations.ZeroMemory(stored.AsSpan());}
        }
        catch(HttpRequestException){throw new HubException("RetroAchievements is unavailable. Check your connection; cached records were kept.");}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new HubException("RetroAchievements timed out. Cached records were kept; try again later.");}
        catch(IOException){throw new HubException("RetroAchievements could not finish reading the response. Cached records were kept.");}
    }
    internal static void RequireSuccess(JsonElement root,string message)
    {if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("Success",out var success)||success.ValueKind!=JsonValueKind.True)throw new HubException(message);}
    public void Dispose(){if(ownsClient)client.Dispose();}
}
