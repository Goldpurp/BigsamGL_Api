using Api.Configuration;
using Api.Extensions;

LocalEnvironmentFile.LoadForCurrentProcess();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBigsamApi(builder.Configuration, builder.Environment);

var app = builder.Build();
await app.InitializeBigsamAsync();
app.UseBigsamPipeline();
await app.RunAsync();

public partial class Program;
