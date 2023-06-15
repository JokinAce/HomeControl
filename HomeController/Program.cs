using HomeController.ESP32;
using Microsoft.AspNetCore.Mvc;
using HomeController;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Prepare HttpClient
//HttpClientHandler httpClientHandler = new();
//httpClientHandler.ServerCertificateCustomValidationCallback += (sender, certificate, chain, errors) => {
//    if (certificate?.GetCertHashString() == "3FA6EA0EA102038122F7F6E2CE52F39C1FAEE0B2")
//        return true;

//    return true;
//};  Old Version used https

HttpClient httpClient = new();
httpClient.DefaultRequestHeaders.Add("apiKey", "PLACEHOLDER");


// Prepare Resources
Boiler boiler = new(httpClient);

// Do timed Readings
_ = new Timer(async (state) => {
    await boiler.Get();
}, null, 0, 300000);


// API Endpoints
string _apiKey = "PLACEHOLDER";

app.MapGet("/boiler/get", ([FromHeader] string? apiKey) => {
    if (_apiKey != apiKey) return Results.Unauthorized();

    string response = $"{boiler?.Status?.IsRelayOn},{boiler?.Status?.CurrentTemp},{boiler?.Status?.ManualMode}";
    response = Obfuscator.Encrypt(response);


    return Results.Text(response, "text/plain");
});

app.MapGet("/boiler/set", ([FromHeader] string? apiKey, [FromQuery] string obfuscated) => {
    if (_apiKey != apiKey) return Results.Unauthorized();

    Obfuscator.Content deobfuscated = Obfuscator.Decrypt(obfuscated);
    if (deobfuscated.IsReplay())
        return Results.Unauthorized();

    _ = Task.Run(async () => {
        string[] settings = deobfuscated.ContentMessage.Split(',');
        await boiler.Set(settings[0] == "True", settings[1] == "True");
    });


    return Results.Ok();
});

app.Run();
