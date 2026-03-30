using Microsoft.EntityFrameworkCore;
using TriviaGame.Data;
using TriviaGame.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddDbContext<TriviaGameContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TriviaGameContext")));
builder.Services.AddSession();

var app = builder.Build();

// Seed database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TriviaGameContext>();

    db.Database.Migrate();

    if (!db.Quizzes.Any())
    {
        var quiz = new Quiz
        {
            Title = "Sample Trivia",
            Description = "A starter quiz for testing the game flow.",
            IsPublished = true,
            CreatedAt = DateTime.Now
        };

        db.Quizzes.Add(quiz);
        db.SaveChanges();

        db.Questions.AddRange(
            new Question
            {
                QuizId = quiz.Id,
                QuestionText = "What is the capital of France?",
                AnswerA = "Paris",
                AnswerB = "London",
                AnswerC = "Berlin",
                AnswerD = "Rome",
                CorrectAnswer = "A",
                Points = 100,
                TimeLimitSeconds = 20
            },
            new Question
            {
                QuizId = quiz.Id,
                QuestionText = "Which planet is known as the Red Planet?",
                AnswerA = "Earth",
                AnswerB = "Mars",
                AnswerC = "Jupiter",
                AnswerD = "Venus",
                CorrectAnswer = "B",
                Points = 100,
                TimeLimitSeconds = 20
            },
            new Question
            {
                QuizId = quiz.Id,
                QuestionText = "How many continents are there?",
                AnswerA = "5",
                AnswerB = "6",
                AnswerC = "7",
                AnswerD = "8",
                CorrectAnswer = "C",
                Points = 100,
                TimeLimitSeconds = 20
            }
        );

        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();