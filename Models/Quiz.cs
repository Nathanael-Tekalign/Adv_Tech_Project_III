using Microsoft.AspNetCore.Mvc;

namespace TriviaGame.Models
{
    public class Quiz
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPublished { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<GameSession> GameSessions { get; set; } = new List<GameSession>();
    }
}
