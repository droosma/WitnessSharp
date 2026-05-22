using WitnessSharp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .WithStandardInstrumentations()
    .WithOtlpExporter()
    .WithConsoleExporter();

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
