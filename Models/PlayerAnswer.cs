using Microsoft.AspNetCore.Mvc;

namespace TriviaGame.Models
{
    public class PlayerAnswer
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public int QuestionId { get; set; }
        public string SelectedAnswer { get; set; } = string.Empty; // "A", "B", "C", or "D"
        public bool IsCorrect { get; set; }
        public int PointsAwarded { get; set; }
        public double TimeTakenSeconds { get; set; }

        public Player Player { get; set; } = null!;
        public Question Question { get; set; } = null!;
    }
}
