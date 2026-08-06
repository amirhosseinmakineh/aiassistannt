using AiAssistant.ApplicationService.Contract.IService;
using AiAssistant.ApplicationService.Services;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);


// MVC Controllers
builder.Services.AddControllers();


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
builder.Services.AddScoped<IOpernAiService, OpenAiService>();


var app = builder.Build();


// HTTPS
app.UseHttpsRedirection();


// اجازه خواندن wwwroot/index.html
app.UseStaticFiles();


app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});


// Routing
app.UseRouting();


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