using Microsoft.AspNetCore.Mvc;

namespace TriviaGame.Models
{
    public class Question
    {
        public int Id { get; set; }
        public int QuizId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string AnswerA { get; set; } = string.Empty;
        public string AnswerB { get; set; } = string.Empty;
        public string AnswerC { get; set; } = string.Empty;
        public string AnswerD { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty; // "A", "B", "C", or "D"
        public int Points { get; set; } = 100;
        public int TimeLimitSeconds { get; set; } = 20;

        public Quiz Quiz { get; set; } = null!;
        public ICollection<PlayerAnswer> PlayerAnswers { get; set; } = new List<PlayerAnswer>();
    }
}
