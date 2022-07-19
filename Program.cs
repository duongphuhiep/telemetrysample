using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// download https://www.jaegertracing.io/download/
// run the server and activate the otlp collector
// .\jaeger-all-in-one.exe --collector.otlp.enabled=true --collector.otlp.grpc.host-port=:4317  --collector.otlp.http.host-port=:4318

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


#region setup OpenTelemetry provider

const string SOURCE_NAME = "Lemonway.Paysquad.Weather";

builder.Services.AddOpenTelemetryTracing(b => b
    .AddAspNetCoreInstrumentation(cfg =>
        //collect all requests except the requests to the swagger UI
        cfg.Filter = httpContext => httpContext.Request.Path.Value != null
                        && !httpContext.Request.Path.Value.Contains("swagger")
                        && !httpContext.Request.Path.Value.Contains("_framework"))
    .AddHttpClientInstrumentation()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                serviceName: "TelemetrySample",
                serviceVersion: "1.0.0"))
    .AddSource(SOURCE_NAME)
    .AddConsoleExporter()
    .AddOtlpExporter(conf =>
    {
        conf.Endpoint = new Uri("http://localhost:4317");
        conf.ExportProcessorType = OpenTelemetry.ExportProcessorType.Simple;
        conf.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
    }));

#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();


ActivitySource MyActivitySource = new(SOURCE_NAME);

app.MapGet("/createPaymentWithDetailTelemetry", async () =>
{
    var client = new HttpClient();

    using (MyActivitySource.StartActivity("Legacy.GetConfig"))
    {
        await client.GetStringAsync("https://httpstat.us/200?sleep=1000");
    }

    using (MyActivitySource.StartActivity("TransactionService.ComputeCommission"))
    {
        await client.GetStringAsync("https://httpstat.us/301"); //this will make 2 https requests to return the response because the target URI returns a redirection HTTP 301
    }

    using (MyActivitySource.StartActivity("Database.CreatePendingPayment"))
    {
        await Task.Delay(200);
    }
    return "OK";
});

app.MapGet("/createPayment", async () =>
{
    var client = new HttpClient();

    //Legacy.GetConfig"
    await client.GetStringAsync("https://httpstat.us/200?sleep=1000");

    //"TransactionService.ComputeCommission"
    await client.GetStringAsync("https://httpstat.us/301"); //this will make 2 https requests to return the response because the target URI returns a redirection HTTP 301

    //CreatePendingPayment
    await Task.Delay(200);

    return "OK";
});

app.Run();
