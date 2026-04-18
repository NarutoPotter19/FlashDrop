using FlashDrop.API.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);




// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();

//Task: Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

//added a cutome Exceptional handling middle ware jsut before HTTPRedirection
app.UseMiddleware<ExceptionHandlingMiddleware>();


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// TEMPORARY — delete after testing
app.MapGet("/test-404", () => {
    throw new FlashDrop.API.Shared.Exceptions.NotFoundException("Testing 404 response");
});

app.Run();
