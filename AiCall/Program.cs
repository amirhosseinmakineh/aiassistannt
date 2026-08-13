using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Services;
using AiAssistant.ApplicationService.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AiAssistantDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AiAssistant")));
builder.Services.AddScoped<IAiLeadPersistenceService, AiLeadPersistenceService>();

// MVC Controllers
builder.Services.AddControllers();
builder.Services.AddAuthorization();


// Swagger
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Dental Realtime API",
        Version = "v1",
        Description = "Dental Voice Assistant Test"
    });
});

builder.Services.Configure<AiAssistant.ApplicationService.Contract.Options.OpenAiRealtimeOptions>(
    builder.Configuration.GetSection("OpenAi"));
builder.Services.AddTransient<IOpenAiRealtimeSessionFactory, OpenAiRealtimeSessionFactory>();
builder.Services.AddHttpClient<IOpenAiCustomVoiceService, OpenAiCustomVoiceService>(client =>
    client.BaseAddress = new Uri("https://api.openai.com/v1/"));


var app = builder.Build();

app.Logger.LogInformation("Starting Dental Realtime API");


app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// WebSocket clients do not follow HTTP 307 redirects. Let upgrade requests reach
// the controller on both configured development URLs, while redirecting normal
// HTTP page/API traffic to HTTPS.
app.UseWhen(
    context => !context.WebSockets.IsWebSocketRequest,
    branch => branch.UseHttpsRedirection());


// اجازه خواندن wwwroot/index.html
app.UseStaticFiles();


// Routing
app.UseRouting();
app.UseAuthorization();


// Swagger
app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Dental Realtime API v1");

    options.RoutePrefix = "swagger";
});


// Controller ها
app.MapControllers();


// صفحه اصلی
app.MapFallbackToFile("index.html");


app.Run();
